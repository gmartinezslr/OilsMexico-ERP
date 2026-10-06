using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Compras : ComponentBase
{
    private string estado = "", texto = "", msg = "";
    private string folioProv = "", notas = "", busq = "";
    private string motivoCancel = "", pagoForma = "03", pagoRef = "";
    private bool err, cargando, guardando, recibiendo, pagando, cancelando, creando, mostrarCxP;
    private List<CompraListadoDto> lista = [];
    private List<CuentasPorPagarDto> cxp = [];
    private List<ProvRow> proveedores = [];
    private List<ProductoDto> busqProds = [];
    private List<CarritoCompra> carrito = [];
    private CompraListadoDto? sel;
    private CompraDetalleDto? detalle;
    private int provId;
    private decimal pagoMonto;

    private readonly Dictionary<int, string> cantRec = new();
    private readonly Dictionary<int, string> loteRec = new();
    private readonly Dictionary<int, string> cadRec = new();

    private sealed record ProvRow(int Id, string Nombre);
    private sealed class CarritoCompra
    {
        public int ProductoId { get; set; }
        public int? UnidadId { get; set; }
        public string Sku { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string UnidadNombre { get; set; } = "Litro";
        public decimal Factor { get; set; } = 1m;
        public decimal Cantidad { get; set; } = 1;
        public decimal Costo { get; set; }
    }

    private decimal SubtotalCarrito => carrito.Sum(r => r.Cantidad * r.Costo);
    private decimal IvaCarrito => Math.Round(SubtotalCarrito * 0.16m, 2);
    private decimal TotalCarrito => SubtotalCarrito + IvaCarrito;

    protected override async Task OnInitializedAsync()
    {
        proveedores = await Db.Proveedores.AsNoTracking().Where(p => p.Activo)
            .OrderBy(p => p.Nombre).Select(p => new ProvRow(p.Id, p.Nombre)).ToListAsync();
        if (proveedores.Count > 0) provId = proveedores[0].Id;
        await Cargar();
        await CargarCxp();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        await Cargar();
        await CargarCxp();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try { lista = await ComprasSvc.ListarAsync(SucCtx.SucursalId, estado, texto); msg = ""; }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task CargarCxp()
    {
        try { cxp = await ComprasSvc.CuentasPorPagarAsync(SucCtx.SucursalId, null); }
        catch { }
    }

    private async Task AlEscribir(ChangeEventArgs e) { texto = e.Value?.ToString() ?? ""; await Cargar(); }
    private void VerCxP() => mostrarCxP = !mostrarCxP;

    private async Task Expandir(int id)
    {
        sel = lista.FirstOrDefault(c => c.CompraId == id);
        cancelando = false;
        cantRec.Clear(); loteRec.Clear(); cadRec.Clear();
        try
        {
            detalle = await ComprasSvc.DetalleAsync(id);
            if (detalle is null) { msg = "Compra no encontrada (o es de otra sucursal)."; err = true; sel = null; }
            else pagoMonto = detalle.Saldo;
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private void NuevaOrden()
    {
        creando = true; sel = null; detalle = null;
        carrito = []; busq = ""; busqProds = [];
        folioProv = ""; notas = ""; msg = "";
    }

    private async Task BuscarProd(ChangeEventArgs e)
    {
        busq = e.Value?.ToString() ?? "";
        busqProds = string.IsNullOrWhiteSpace(busq) ? [] : await Ventas.BuscarProductosAsync(SucCtx.SucursalId, busq);
    }

    private void AgregarRenglon(ProductoDto p)
    {
        var ex = carrito.FirstOrDefault(r => r.ProductoId == p.Id);
        if (ex is null)
            carrito.Add(new CarritoCompra
            {
                ProductoId = p.Id, Sku = p.Sku, Nombre = p.Nombre,
                UnidadNombre = p.UnidadVenta, Factor = p.FactorConversion,
                Cantidad = 1, Costo = p.PrecioVenta
            });
        else ex.Cantidad++;
        busq = ""; busqProds = [];
    }

    private void QuitarRenglon(CarritoCompra r) => carrito.Remove(r);

    private async Task GuardarOrden()
    {
        msg = "";
        if (carrito.Count == 0) { msg = "Agrega al menos un renglón."; err = true; return; }
        guardando = true;
        try
        {
            var req = new CrearCompraRequest(SucCtx.SucursalId, provId, folioProv, notas,
                carrito.Select(r => new CompraRenglonDto(
                    r.ProductoId, r.UnidadId, r.UnidadNombre, r.Factor, r.Cantidad, r.Costo)).ToList());
            var res = await ComprasSvc.CrearOrdenAsync(req);
            msg = $"Orden {res.FolioInterno} creada. Total ${res.Total:N2}."; err = false;
            creando = false;
            await Cargar(); await CargarCxp();
            await Expandir(res.CompraId);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private string CantRec(int id) => cantRec.TryGetValue(id, out var v) ? v : "";
    private string LoteRec(int id) => loteRec.TryGetValue(id, out var v) ? v : "";
    private string CadRec(int id) => cadRec.TryGetValue(id, out var v) ? v : "";
    private void SetCantRec(int id, string? v) => cantRec[id] = v ?? "";
    private void SetLoteRec(int id, string? v) => loteRec[id] = v ?? "";
    private void SetCadRec(int id, string? v) => cadRec[id] = v ?? "";

    private async Task ConfirmarRecepcion()
    {
        msg = "";
        if (sel is null) return;
        var renglones = new List<RecepcionRenglonDto>();
        foreach (var kv in cantRec)
        {
            if (!decimal.TryParse(kv.Value, out var cant) || cant <= 0) continue;
            DateOnly? cad = null;
            if (cadRec.TryGetValue(kv.Key, out var cs) && DateOnly.TryParse(cs, out var cd)) cad = cd;
            loteRec.TryGetValue(kv.Key, out var lote);
            renglones.Add(new RecepcionRenglonDto(kv.Key, cant, lote, cad));
        }
        if (renglones.Count == 0) { msg = "Captura al menos una cantidad a recibir."; err = true; return; }
        recibiendo = true;
        try
        {
            var res = await ComprasSvc.RecibirAsync(new RecepcionRequest(sel.CompraId, renglones));
            msg = $"Recepción aplicada. Estado: {res.Estado}."; err = false;
            await Cargar();
            await Expandir(sel.CompraId);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        recibiendo = false;
    }

    private void EmpezarCancel() { cancelando = true; motivoCancel = ""; }

    private async Task ConfirmarCancel()
    {
        msg = "";
        if (sel is null) return;
        try
        {
            var res = await ComprasSvc.CancelarAsync(sel.CompraId, motivoCancel);
            msg = $"Orden {res.FolioInterno} cancelada."; err = false;
            cancelando = false;
            await Cargar(); await CargarCxp();
            await Expandir(sel.CompraId);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private async Task RegistrarPago()
    {
        msg = "";
        if (sel is null || detalle is null) return;
        pagando = true;
        try
        {
            await ComprasSvc.RegistrarPagoAsync(new PagoCompraRequest(sel.CompraId, pagoMonto, pagoForma, pagoRef));
            msg = "Pago registrado."; err = false;
            pagoRef = "";
            await Cargar(); await CargarCxp();
            await Expandir(sel.CompraId);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        pagando = false;
    }

    private static string ClaseEstado(string e) => e switch
    {
        "Borrador" => "bg-secondary",
        "Parcial" => "bg-warning text-dark",
        "Recibida" => "bg-success",
        "Cancelada" => "bg-danger",
        _ => "bg-secondary"
    };

    private static string ClasePago(string e) => e switch
    {
        "Pagada" => "bg-success",
        "Parcial" => "bg-warning text-dark",
        "Pendiente" => "bg-dark",
        _ => "bg-secondary"
    };
}

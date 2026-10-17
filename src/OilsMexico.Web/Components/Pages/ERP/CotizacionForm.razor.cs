using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class CotizacionForm : ComponentBase
{
    private sealed class Renglon
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string Sku { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int UnidadMedidaId { get; set; }
        public decimal Cantidad { get; set; } = 1;
        public decimal Precio { get; set; }
        public decimal Descuento { get; set; }
    }

    [Parameter] public int Id { get; set; }
    private string Titulo => Id > 0 ? "Editar cotización" : "Nueva cotización";

    private List<(int Id, string Nombre)> clientes = [];
    private List<(int Id, string Nombre)> vendedores = [];
    private List<Renglon> renglones = [];
    private int clienteId;
    private int vendedorId;
    private CreditoClienteDto? creditoCliente;
    private bool consultandoCredito;
    private string? errorCredito;
    private string? errorPrecio;
    private DateTime fechaVencimiento = DateTime.UtcNow.AddDays(7);
    private string notas = "";
    private string filtroProducto = "";
    private List<ProductoDto> productos = [];
    private string marcaVehiculo = "", modeloVehiculo = "";
    private int anioVehiculo = DateTime.UtcNow.Year;
    private List<CompatibilidadVehiculoDto> compatibilidades = [];
    private string? errorCompatibilidad;
    private List<ProductoDto> productosFiltrados =>
        productos.Where(p => p.Sku.Contains(filtroProducto) || p.Nombre.Contains(filtroProducto)).ToList();

    private bool cargando;
    private bool guardando;
    private string msg = "";
    private bool err;

    private decimal Subtotal => renglones.Sum(r => r.Cantidad * r.Precio - r.Descuento);
    private decimal Iva => Impuestos.IvaDeBase(Subtotal);
    private decimal Total => Math.Round(Subtotal + Iva, 2);

    [Inject] public ICotizacionesConsulta CotizacionesConsulta { get; set; } = default!;
    [Inject] public ICotizacionesOperaciones CotizacionesOps { get; set; } = default!;
    [Inject] public IVentasPosService Ventas { get; set; } = default!;
    [Inject] public IDiferenciadoresService Diferenciadores { get; set; } = default!;
    [Inject] public IAutorizacionService Autorizacion { get; set; } = default!;
    [Inject] public ISucursalContext SucCtx { get; set; } = default!;
    [Inject] public ISesionActual Sesion { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync())
        {
            Nav.NavigateTo("/login");
            return;
        }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("cotizaciones")))
        {
            Nav.NavigateTo("/");
            return;
        }
        if (Id > 0)
        {
            cargando = true;
            try
            {
                var detalle = await CotizacionesConsulta.ObtenerCotizacionAsync(Id);
                if (detalle is null)
                {
                    Nav.NavigateTo("/cotizaciones", forceLoad: true);
                    return;
                }
                clienteId = detalle.ClienteId;
                vendedorId = detalle.VendedorId ?? 0;
                fechaVencimiento = detalle.FechaVencimiento?.ToLocalTime() ?? DateTime.UtcNow.AddDays(7);
                notas = detalle.Notas ?? "";
                renglones = detalle.Detalles.Select(d => new Renglon
                {
                    Id = d.Id, ProductoId = d.ProductoId, Sku = d.Sku, Nombre = d.Nombre,
                    UnidadMedidaId = d.UnidadMedidaId, Cantidad = d.Cantidad,
                    Precio = d.PrecioUnitario, Descuento = d.Descuento
                }).ToList();
                msg = "";
            }
            catch (Exception ex)
            {
                msg = "Error: " + ex.Message;
                err = true;
            }
            finally
            {
                cargando = false;
            }
        }
        else
        {
            renglones = [new Renglon()];
        }

        productos = await CotizacionesConsulta.ListarProductosAsync(SucCtx.SucursalId);
        await CargarClientes();
        await CargarVendedores();
        await ConsultarCreditoClienteAsync();
        StateHasChanged();
    }

    private string GuardandoTexto => guardando ? "Guardando…" : (Id > 0 ? "Guardar cambios" : "Crear cotización");

    private async Task CargarClientes()
    {
        clientes = await CotizacionesConsulta.ListarClientesAsync(SucCtx.SucursalId);
        if (clientes.Count > 0 && clienteId == 0) clienteId = clientes[0].Id;
        StateHasChanged();
    }

    private async Task CargarVendedores()
    {
        vendedores = await CotizacionesConsulta.ListarVendedoresAsync(SucCtx.SucursalId);
        if (vendedores.Count > 0 && vendedorId == 0) vendedorId = vendedores[0].Id;
        StateHasChanged();
    }

    private async Task CargarSucursales()
    {
        await ConsultarCreditoClienteAsync();
        foreach (var renglon in renglones.Where(x => x.ProductoId > 0 && x.Cantidad > 0))
            await ActualizarPrecio(renglon);
        StateHasChanged();
    }

    private async Task BuscarCompatibilidad()
    {
        errorCompatibilidad = null;
        try
        {
            compatibilidades = await Diferenciadores.BuscarCompatibilidadAsync(marcaVehiculo, modeloVehiculo, anioVehiculo);
            if (compatibilidades.Count == 0) errorCompatibilidad = "No se encontraron aplicaciones para esos datos.";
        }
        catch (Exception ex) { errorCompatibilidad = $"No se pudo consultar compatibilidad: {ex.Message}"; }
    }

    private async Task ConsultarCreditoClienteAsync()
    {
        creditoCliente = null;
        errorCredito = null;
        if (clienteId <= 0) return;

        consultandoCredito = true;
        try
        {
            creditoCliente = await Ventas.ConsultarCreditoClienteAsync(clienteId);
        }
        catch (Exception ex)
        {
            errorCredito = $"No se pudo consultar el crédito: {ex.Message}";
        }
        finally
        {
            consultandoCredito = false;
        }
    }

    private async Task Guardar()
    {
        if (clienteId == 0) { msg = "Selecciona un cliente."; err = true; return; }
        var validos = renglones.Where(r => r.ProductoId > 0 && r.Cantidad > 0).ToList();
        if (validos.Count == 0) { msg = "Agrega al menos un producto."; err = true; return; }

        guardando = true;
        msg = ""; err = false;
        try
        {
            var detalles = validos.Select(r => new CotizacionRenglonCrearDto(
                r.ProductoId, r.UnidadMedidaId > 0 ? r.UnidadMedidaId : null,
                r.Cantidad, r.Precio, r.Descuento)).ToList();

            if (Id > 0)
            {
                await CotizacionesOps.ActualizarCotizacionAsync(Id, new CotizacionCrearRequest(
                    clienteId, vendedorId > 0 ? vendedorId : null, fechaVencimiento, notas, detalles));
                msg = "Cotización actualizada.";
            }
            else
            {
                var res = await CotizacionesOps.CrearCotizacionAsync(new CotizacionCrearRequest(
                    clienteId, vendedorId > 0 ? vendedorId : null, fechaVencimiento, notas, detalles));
                msg = $"Cotización {res.FolioInterno} creada.";
            }
            Nav.NavigateTo("/cotizaciones", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            guardando = false;
        }
    }

    private void FiltrarProductos(ChangeEventArgs e)
    {
        filtroProducto = e.Value?.ToString() ?? "";
        StateHasChanged();
    }

    private void AgregarRenglon()
    {
        var r = renglones.LastOrDefault();
        renglones.Add(r is not null && r.ProductoId > 0
            ? new Renglon()
            : new Renglon());
    }

    private void EliminarRenglon(int i)
    {
        if (renglones.Count <= 1) return;
        renglones.RemoveAt(i);
    }

    private async Task AsignarPrecio(int i)
    {
        if (i < 0 || i >= renglones.Count) return;
        var r = renglones[i];
        var prod = productos.FirstOrDefault(p => p.Id == r.ProductoId);
        if (prod is null) return;
        r.Sku = prod.Sku;
        r.Nombre = prod.Nombre;
        r.UnidadMedidaId = prod.UnidadMedidaId;
        await ActualizarPrecio(r);
    }

    private async Task ActualizarPrecio(Renglon renglon)
    {
        if (renglon.ProductoId <= 0 || clienteId <= 0 || renglon.Cantidad <= 0) return;
        try
        {
            errorPrecio = null;
            var factor = productos.FirstOrDefault(p => p.Id == renglon.ProductoId)?.FactorConversion ?? 1m;
            var resuelto = await Diferenciadores.ResolverPrecioAsync(
                renglon.ProductoId, clienteId, renglon.Cantidad, factor);
            renglon.Precio = resuelto.Precio;
        }
        catch (Exception ex) { errorPrecio = $"No se pudo calcular el precio sugerido: {ex.Message}"; }
    }
}

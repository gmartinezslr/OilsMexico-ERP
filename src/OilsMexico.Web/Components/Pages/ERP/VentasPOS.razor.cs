using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Web.Hubs;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class VentasPOS : ComponentBase
{
    [Inject] public IVentasService Ventas { get; set; } = default!;
    [Inject] public ICatalogosSatService Sat { get; set; } = default!;
    [Inject] public ISucursalContext SucursalCtx { get; set; } = default!;
    [Inject] public ErpDbContext Db { get; set; } = default!;
    [Inject] public IHubContext<ErpHub> Hub { get; set; } = default!;
    [Inject] public ISesionActual Sesion { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;
    [Inject] public ICorteCajaService Caja { get; set; } = default!;
    protected CorteAbiertoDto? corteAbierto;

    protected string filtro = "";
    protected List<ProductoDto> productos = [];
    protected List<CarritoItemDto> carrito = [];
    protected List<(int Id, string Nombre)> sucursales = [];
    protected List<(int Id, string Nombre)> clientes = [];
    protected List<(int Id, string Nombre)> vendedores = [];
    protected Dictionary<int, int> vendedorPorCliente = [];
    protected int? vendedorId;
    protected CatalogosSatDto catalogos = new([], [], [], []);
    protected string formaPago = "01", metodoPago = "PUE", usoCfdi = "G03";
    protected int clienteId = 1;
    protected bool requiereFactura = false, procesando = false, esError = false;
    protected string mensaje = "";
    protected VentaPosResult? ultimo;
    // Guarda contra búsquedas superpuestas (escribir rápido / doble render): una segunda
    // operación sobre el mismo DbContext del circuito tumba Blazor con InvalidOperationException.
    private int _busquedaEnCurso;
    private string _ultimoFiltroBuscado = "\u0001";

    protected decimal SubtotalBruto => carrito.Sum(i => i.Importe);
    protected decimal Subtotal => Impuestos.BaseDeTotal(SubtotalBruto);
    protected decimal Iva => Impuestos.IvaDeTotal(SubtotalBruto);
    protected decimal Total => Math.Round(SubtotalBruto, 2);

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        catalogos = Sat.Obtener();
        // NOTA: no se consulta la BD aquí. Las lecturas se hacen en OnAfterRenderAsync
        // (circuito ya interactivo y sesión restaurada) para no chocar con el prerender
        // ni con otras consultas concurrentes del mismo DbContext del circuito.
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login", forceLoad: true); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor")) { Nav.NavigateTo("/"); return; }
        try
        {
            sucursales = await Db.Sucursales.AsNoTracking().Select(s => new ValueTuple<int, string>(s.Id, s.Nombre)).ToListAsync();
            clientes = await Db.Clientes.AsNoTracking().Select(c => new ValueTuple<int, string>(c.Id, c.Nombre)).ToListAsync();
            vendedores = await Db.Usuarios.AsNoTracking()
                .Where(u => u.Activo && (u.Rol == "Vendedor" || u.Rol == "Admin"))
                .OrderBy(u => u.Nombre)
                .Select(u => new ValueTuple<int, string>(u.Id, u.Nombre)).ToListAsync();
            // Dueño comercial de cada cliente: es el vendedor SUGERIDO al facturar (editable).
            var duenos = await Db.Clientes.AsNoTracking()
                .Select(c => new { c.Id, c.VendedorId }).ToListAsync();
            vendedorPorCliente = duenos.Where(x => x.VendedorId != null)
                .ToDictionary(x => x.Id, x => x.VendedorId!.Value);
            if (clientes.Count > 0) clienteId = clientes[0].Id;
            vendedorId = vendedorPorCliente.GetValueOrDefault(clienteId);
            await Buscar();
            try { corteAbierto = await Caja.AbiertoAsync(SucursalCtx.SucursalId); } catch { }
        }
        catch (InvalidOperationException)
        {
            // Circuito/JS aún estabilizándose: reintentar en el siguiente render, sin tumbar Blazor.
            try { StateHasChanged(); } catch { }
            return;
        }
        StateHasChanged();
    }

    /// <summary>Al cambiar el cliente se preselecciona su vendedor dueño (sugerido, editable).</summary>
    protected void OnClienteCambiado()
    {
        vendedorId = vendedorPorCliente.GetValueOrDefault(clienteId);
    }

    protected async Task Buscar()
    {
        // Serie: si ya hay una búsqueda en vuelo, se marca el filtro pendiente y esa misma
        // búsqueda encadena la actualización al terminar (sin solapar operaciones en el DbContext).
        var miFiltro = filtro;
        if (System.Threading.Interlocked.Exchange(ref _busquedaEnCurso, 1) == 1)
        {
            _ultimoFiltroBuscado = miFiltro;
            return;
        }
        try
        {
            do
            {
                _ultimoFiltroBuscado = miFiltro;
                productos = await Ventas.BuscarProductosAsync(SucursalCtx.SucursalId, miFiltro);
                miFiltro = _ultimoFiltroBuscado;
            } while (miFiltro != filtro);
            try { await InvokeAsync(StateHasChanged); } catch { }
        }
        finally { System.Threading.Interlocked.Exchange(ref _busquedaEnCurso, 0); }
    }

    protected async Task AlEscribir(ChangeEventArgs e)
    {
        filtro = e.Value?.ToString() ?? "";
        await Buscar();
    }

    protected async Task CambiarSucursal(ChangeEventArgs e)
    {
        // Solo Admin puede cambiar de sucursal; el resto opera en la suya (Regla #1).
        if (Sesion.Sesion?.Rol == "Admin")
            SucursalCtx.Establecer(int.Parse(e.Value!.ToString()!), Sesion.Sesion.UsuarioId, "Admin");
        carrito.Clear();
        await Buscar();
    }

    protected void Agregar(ProductoDto p)
    {
        var ex = carrito.FirstOrDefault(i => i.ProductoId == p.Id);
        if (ex is null)
            carrito.Add(new CarritoItemDto(p.Id, p.Sku, p.Nombre, 0, p.UnidadVenta, p.FactorConversion, 1, p.PrecioVenta));
        else
            carrito[carrito.IndexOf(ex)] = ex with { Cantidad = ex.Cantidad + 1 };
    }

    protected void Quitar(CarritoItemDto it) => carrito.Remove(it);

    protected async Task Cobrar()
    {
        procesando = true; mensaje = "";
        try
        {
            var req = new VentaPosRequest(SucursalCtx.SucursalId, clienteId, Sesion.Sesion!.UsuarioId,
                vendedorId, formaPago, metodoPago, usoCfdi, requiereFactura, carrito);
            ultimo = await Ventas.RegistrarVentaAsync(req);
            mensaje = $"Venta {ultimo.FolioInterno} registrada ({ultimo.Estado}).";
            esError = false;
            carrito.Clear();
            await Hub.Clients.Group($"sucursal-{SucursalCtx.SucursalId}")
                .SendAsync("venta-nueva", ultimo.FolioInterno, ultimo.Total);
            await Buscar();
        }
        catch (Exception ex) { mensaje = "Error: " + ex.Message; esError = true; }
        procesando = false;
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
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
    protected CatalogosSatDto catalogos = new([], [], [], []);
    protected string formaPago = "01", metodoPago = "PUE", usoCfdi = "G03";
    protected int clienteId = 1;
    protected bool requiereFactura = false, procesando = false, esError = false;
    protected string mensaje = "";
    protected VentaPosResult? ultimo;

    protected decimal SubtotalBruto => carrito.Sum(i => i.Importe);
    protected decimal Subtotal => Math.Round(SubtotalBruto / 1.16m, 2);
    protected decimal Iva => Math.Round(SubtotalBruto - Subtotal, 2);
    protected decimal Total => Math.Round(SubtotalBruto, 2);

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        catalogos = Sat.Obtener();
        sucursales = await Db.Sucursales.Select(s => new ValueTuple<int, string>(s.Id, s.Nombre)).ToListAsync();
        clientes = await Db.Clientes.Select(c => new ValueTuple<int, string>(c.Id, c.Nombre)).ToListAsync();
        if (clientes.Count > 0) clienteId = clientes[0].Id;
        await Buscar();
        try { corteAbierto = await Caja.AbiertoAsync(SucursalCtx.SucursalId); } catch { }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login", forceLoad: true); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor")) { Nav.NavigateTo("/"); return; }
        await Buscar();
        StateHasChanged();
    }

    protected async Task Buscar()
    {
        productos = await Ventas.BuscarProductosAsync(SucursalCtx.SucursalId, filtro);
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
                formaPago, metodoPago, usoCfdi, requiereFactura, carrito);
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

using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using Microsoft.AspNetCore.Components;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class VentasGestion
{
    [Inject] private IGestionService Gestion { get; set; } = default!;
    [Inject] private IEstadoCuentasService EstadoCuentas { get; set; } = default!;
    [Inject] private ISucursalContext SucCtx { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private DateTime Desde { get; set; } = DateTime.Today.AddDays(-30);
    private DateTime Hasta { get; set; } = DateTime.Today;
    private DashboardGestionDto? _dashboard;
    private List<VentasPorDiaDto>? _ventasPorDia;
    private List<RotacionABCDto>? _rotacion;
    private List<CorteSucursalDto>? _cortes;
    private List<ViscosidadDto>? _viscosidad;
    private bool _cargando;

    protected override async Task OnParametersSetAsync()
    {
        if (Sesion.Autenticado && Sesion.Sesion?.Rol is ("Admin" or "Conta"))
            await Cargar();
    }

    // Tras el primer render interactivo se valida sesión y rol: sin sesión → /login y sin permiso → /.
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Conta")) { Nav.NavigateTo("/"); return; }
        if (_dashboard is null) { await Cargar(); StateHasChanged(); }
    }

    private async Task Cargar()
    {
        _cargando = true;
        try
        {
            _dashboard = await Gestion.DashboardAsync(SucCtx.SucursalId, Desde, Hasta);
            _ventasPorDia = await Gestion.VentasPorDiaAsync(SucCtx.SucursalId, Desde, Hasta);
            _rotacion = await Gestion.RotacionAbcAsync(SucCtx.SucursalId, Desde, Hasta);
            _cortes = await Gestion.CortesPorSucursalAsync(SucCtx.SucursalId);
            _viscosidad = await Gestion.InventarioPorViscosidadAsync(SucCtx.SucursalId);
            StateHasChanged();
        }
        finally
        {
            _cargando = false;
        }
    }
}

@using OilsMexico.Application.Interfaces
@using OilsMexico.Application.DTOs
@inject IGestionService Gestion
@inject IEstadoCuentasService EstadoCuentas
@inject ISucursalContext SucCtx
@inject ISesionActual Sesion
@code {
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
        await Cargar();
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

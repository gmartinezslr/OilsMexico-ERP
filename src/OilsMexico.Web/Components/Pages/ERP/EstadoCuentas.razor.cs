@using OilsMexico.Application.Interfaces
@using OilsMexico.Application.DTOs
@inject IEstadoCuentasService EstadoCuentas
@inject ISucursalContext SucCtx
@inject ISesionActual Sesion
@code {
    private List<EstadoCuentaClienteDto>? _estados;
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
            _estados = await EstadoCuentas.ListarEstadosCuentasAsync(SucCtx.SucursalId);
        }
        finally
        {
            _cargando = false;
        }
    }
}

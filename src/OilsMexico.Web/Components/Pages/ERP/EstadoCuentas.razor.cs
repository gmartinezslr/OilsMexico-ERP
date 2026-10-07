using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using Microsoft.AspNetCore.Components;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class EstadoCuentas
{
    [Inject] private IEstadoCuentasService EstadoCuentasSvc { get; set; } = default!;
    [Inject] private ISucursalContext SucCtx { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;

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
            _estados = await EstadoCuentasSvc.ListarEstadosCuentasAsync(SucCtx.SucursalId);
        }
        finally
        {
            _cargando = false;
        }
    }
}

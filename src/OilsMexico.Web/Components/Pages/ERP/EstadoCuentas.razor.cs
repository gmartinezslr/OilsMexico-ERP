using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using Microsoft.AspNetCore.Components;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class EstadoCuentas
{
    [Inject] private IEstadoCuentasService EstadoCuentasSvc { get; set; } = default!;
    [Inject] private ISucursalContext SucCtx { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private List<EstadoCuentaClienteDto>? _estados;
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
        if (_estados is null) { await Cargar(); StateHasChanged(); }
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

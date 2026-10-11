using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;
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
    private List<(int Id, string Nombre)> _clientesPlantilla = [];
    private string? _clienteSel;
    private decimal? _saldoSel;

    protected override async Task OnParametersSetAsync()
    {
        if (Sesion.Autenticado && Sesion.Sesion?.Rol is (Roles.Admin or Roles.Conta))
            await Cargar();
    }

    // Tras el primer render interactivo se valida sesión y rol: sin sesión → /login y sin permiso → /.
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not (Roles.Admin or Roles.Conta)) { Nav.NavigateTo("/"); return; }
        if (_estados is null) { await Cargar(); StateHasChanged(); }
    }

    private async Task Cargar()
    {
        _cargando = true;
        try
        {
            _estados = await EstadoCuentasSvc.ListarEstadosCuentasAsync(SucCtx.SucursalId);
            // Catálogo para plantillas: solo clientes con saldo, ordenados por saldo desc.
            _clientesPlantilla = _estados
                .Where(e => e.SaldoPendiente > 0m)
                .OrderByDescending(e => e.SaldoPendiente)
                .Select(e => (e.ClienteId, e.Nombre))
                .ToList();
            var top = _estados.Where(e => e.SaldoPendiente > 0m).OrderByDescending(e => e.SaldoPendiente).FirstOrDefault();
            _clienteSel = top?.Nombre;
            _saldoSel = top?.SaldoPendiente;
        }
        finally
        {
            _cargando = false;
        }
    }
}

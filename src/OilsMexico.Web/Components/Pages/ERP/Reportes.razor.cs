using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Reportes
{
    [Inject] private IReportesFinancierosService ReportesSvc { get; set; } = default!;
    [Inject] private ISucursalContext SucCtx { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IVentaEscopoFactory Escopos { get; set; } = default!;

    private int _sucursalId;
    private DateTime _desde = DateTime.Today.AddMonths(-1);
    private DateTime _hasta = DateTime.Today;
    private DateTime _corte = DateTime.Today;
    private bool _cargando;
    private string? _errores;
    private List<(int Id, string Nombre)>? _sucursales;

    protected override async Task OnParametersSetAsync()
    {
        if (Sesion.Autenticado && Sesion.Sesion?.Rol is (Roles.Admin or Roles.Conta))
            await CargarSucursales();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not (Roles.Admin or Roles.Conta)) { Nav.NavigateTo("/"); return; }
        if (_sucursales is null || _sucursales.Count == 0) { await CargarSucursales(); StateHasChanged(); }
        if (_desde == default) _desde = DateTime.Today.AddMonths(-1);
        if (_hasta == default) _hasta = DateTime.Today;
        if (_corte == default) _corte = DateTime.Today;
        await Cargar();
    }

    private async Task CargarSucursales()
    {
        try
        {
            _sucursales = await Escopos.ListarAsync(db => db.Sucursales
                .AsNoTracking()
                .OrderBy(s => s.CodigoSucursal)
                .Select(s => new ValueTuple<int, string>(s.Id, s.Nombre)));
        }
        catch { }
    }

    private async Task OnSucursalChange()
    {
        await Cargar();
    }

    private async Task Cargar()
    {
        if (_sucursalId == 0) _sucursalId = SucCtx.SucursalId;
        _cargando = true;
        try
        {
            // Verificar permisos: solo Admin/Conta pueden ver reportes financieros
            if (Sesion.Sesion?.Rol is not (Roles.Admin or Roles.Conta))
            {
                _errores = "No tienes permisos para ver los reportes financieros.";
                return;
            }

            // Aquí se podrían hacer las llamadas al servicio si fuera necesario
            // _balanza = await ReportesSvc.BalanzaAsync(_sucursalId, _desde, _hasta);
            // _balance = await ReportesSvc.BalanceAsync(_sucursalId, _corte);
        }
        catch (Exception ex)
        {
            _errores = $"Error al cargar los reportes: {ex.Message}";
        }
        finally
        {
            _cargando = false;
        }
    }
}

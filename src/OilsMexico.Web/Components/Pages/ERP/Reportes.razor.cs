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
    private string _tab = "balanza";
    private List<(int Id, string Nombre)>? _sucursales;

    // Resultados de cada reporte (null = aún no generados).
    private BalanzaComprobacionDto? _balanza;
    private BalanceGeneralDto? _balance;
    private EstadoResultadosDto? _resultados;
    private CambioExistenciasDto? _existencias;
    private List<IndicadoresSucursalDto>? _indicadores;

    // Sucursal efectiva para los reportes puntuales cuando se eligió "Todas" (0).
    private int SucEfectiva => _sucursalId == 0 ? SucCtx.SucursalId : _sucursalId;

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
        _balanza = null;
        _balance = null;
        _resultados = null;
        _existencias = null;
        _indicadores = null;
        await Cargar();
    }

    private async Task Cargar()
    {
        // Verificar permisos: solo Admin/Conta pueden ver reportes financieros.
        if (Sesion.Sesion?.Rol is not (Roles.Admin or Roles.Conta))
        {
            _errores = "No tienes permisos para ver los reportes financieros.";
            return;
        }

        _cargando = true;
        _errores = null;
        try
        {
            // Balanza (siempre necesaria para el primer tab).
            _balanza ??= await ReportesSvc.BalanzaAsync(SucEfectiva, _desde, _hasta);

            switch (_tab)
            {
                case "balance":
                    _balance ??= await ReportesSvc.BalanceAsync(SucEfectiva, _corte);
                    break;
                case "resultados":
                    _resultados ??= await ReportesSvc.ResultadosAsync(SucEfectiva, _desde, _hasta);
                    break;
                case "existencias":
                    _existencias ??= await ReportesSvc.ExistenciasAsync(SucEfectiva, _desde, _hasta);
                    break;
                case "indicadores":
                    // Con "Todas" (0) un Admin ve todas las sucursales; el resto la suya.
                    _indicadores ??= await ReportesSvc.IndicadoresAsync(_desde, _hasta, _sucursalId == 0 ? null : _sucursalId);
                    break;
            }
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

    // Cambio de pestaña: garantiza que el reporte esté cargado (carga perezosa).
    private async Task SeleccionarTab(string tab)
    {
        _tab = tab;
        await Cargar();
    }

    // Token de sesión para los enlaces de descarga (?t=...), igual que la descarga de XML.
    private string Token => Sesion.Sesion?.SesionToken ?? "";

    // URLs de exportación (§2.1). Cada una apunta a un endpoint /api/export/* que genera el archivo.
    private string UrlBalanzaPdf => $"/api/export/financiero/balanza/{SucEfectiva}?desde={_desde:yyyy-MM-dd}&hasta={_hasta:yyyy-MM-dd}&t={Token}";
    private string UrlBalancePdf => $"/api/export/financiero/balance/{SucEfectiva}?desde={_desde:yyyy-MM-dd}&hasta={_corte:yyyy-MM-dd}&t={Token}";
    private string UrlResultadosPdf => $"/api/export/financiero/resultados/{SucEfectiva}?desde={_desde:yyyy-MM-dd}&hasta={_hasta:yyyy-MM-dd}&t={Token}";
    private string UrlExistenciasPdf => $"/api/export/financiero/existencias/{SucEfectiva}?desde={_desde:yyyy-MM-dd}&hasta={_hasta:yyyy-MM-dd}&t={Token}";
    private string UrlVentasXlsx => $"/api/export/ventas/{SucEfectiva}?desde={_desde:yyyy-MM-dd}&hasta={_hasta:yyyy-MM-dd}&t={Token}";
    private string UrlCxcXlsx => $"/api/export/cxc/{SucEfectiva}?t={Token}";
    private string UrlCxpXlsx => $"/api/export/cxp/{SucEfectiva}?t={Token}";
    private string UrlInventarioXlsx => $"/api/export/inventario/{SucEfectiva}?t={Token}";
    private string UrlGastosXlsx => $"/api/export/gastos/{SucEfectiva}?desde={_desde:yyyy-MM-dd}&hasta={_hasta:yyyy-MM-dd}&t={Token}";
    private string UrlComisionesXlsx => $"/api/export/comisiones?sucursalId={(_sucursalId == 0 ? "" : _sucursalId.ToString())}&desde={_desde:yyyy-MM-dd}&hasta={_hasta:yyyy-MM-dd}&t={Token}";
    private string UrlListaPreciosXlsx => $"/api/export/lista-precios?t={Token}";
}

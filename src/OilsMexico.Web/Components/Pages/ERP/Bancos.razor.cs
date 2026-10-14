using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

/// <summary>
/// Conciliación bancaria: captura el extracto de una cuenta y lo empata con los
/// cobros/pagos del ERP. Protegida para Admin y Conta (ver OnAfterRenderAsync).
/// Inyecciones declaradas en Bancos.razor.
/// </summary>
public partial class Bancos : ComponentBase
{
    [Inject] private IAutorizacionService Autorizacion { get; set; } = default!;
    private List<CuentaBancariaDto> cuentas = [];
    private int cuentaSel;
    private EstadoConciliacion? filtroEstado;

    private List<MovimientoBancarioDto> movimientos = [];
    private ResumenConciliacionDto? resumen;
    private List<MovimientoErpDto> pendientes = [];

    // Alta de cuenta
    private string banco = "", alias = "", numeroCuenta = "", clabe = "", moneda = "MXN";
    private decimal saldoInicial;
    private bool mostrarAltaCuenta;

    // Carga de movimiento del extracto
    private DateTime movFecha = DateTime.Today;
    private string movConcepto = "", movReferencia = "";
    private NaturalezaMovimiento movNaturaleza = NaturalezaMovimiento.Abono;
    private decimal movMonto;

    // Conciliación: movimiento del extracto seleccionado + candidato del ERP
    private int? conciliarMovId;

    private string msg = "";
    private bool err, cargando;

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("bancos"))) { Nav.NavigateTo("/"); return; }
        await CargarCuentas();
        StateHasChanged();
    }

    private async Task CargarCuentas()
    {
        cargando = true;
        try
        {
            cuentas = await BancosSvc.ListarCuentasAsync(SucCtx.SucursalId);
            if (cuentaSel == 0 || cuentas.All(c => c.Id != cuentaSel))
                cuentaSel = cuentas.Count > 0 ? cuentas[0].Id : 0;
            await CargarDetalle();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task CargarDetalle()
    {
        if (cuentaSel == 0)
        {
            movimientos = []; resumen = null; pendientes = [];
            return;
        }
        try
        {
            movimientos = await BancosSvc.ListarMovimientosAsync(cuentaSel, filtroEstado);
            resumen = await BancosSvc.ResumenAsync(cuentaSel);
            pendientes = await BancosSvc.MovimientosErpPendientesAsync(SucCtx.SucursalId);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private async Task CambiarCuenta(ChangeEventArgs e)
    {
        cuentaSel = int.TryParse(e.Value?.ToString(), out var id) ? id : 0;
        conciliarMovId = null;
        await CargarDetalle();
    }

    private async Task CambiarFiltro(ChangeEventArgs e)
    {
        var v = e.Value?.ToString();
        filtroEstado = string.IsNullOrEmpty(v) ? null
            : Enum.TryParse<EstadoConciliacion>(v, out var est) ? est : null;
        await CargarDetalle();
    }

    private async Task CrearCuenta()
    {
        msg = "";
        try
        {
            await BancosSvc.CrearCuentaAsync(new CrearCuentaBancariaRequest(
                SucCtx.SucursalId, banco, alias, numeroCuenta, clabe, moneda, saldoInicial));
            msg = "Cuenta creada."; err = false;
            banco = alias = numeroCuenta = clabe = ""; saldoInicial = 0; moneda = "MXN";
            mostrarAltaCuenta = false;
            await CargarCuentas();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private async Task CargarMovimiento()
    {
        msg = "";
        if (cuentaSel == 0) { msg = "Primero crea o selecciona una cuenta."; err = true; return; }
        try
        {
            await BancosSvc.CargarMovimientoAsync(new CargarMovimientoRequest(
                cuentaSel, movFecha, movConcepto, movReferencia, movNaturaleza, movMonto));
            msg = "Movimiento del extracto cargado."; err = false;
            movConcepto = ""; movReferencia = ""; movMonto = 0;
            await CargarDetalle();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private void EmpezarConciliar(int movimientoId) => conciliarMovId = movimientoId;

    // Los candidatos se filtran por la naturaleza del movimiento (Abono→Cobro, Cargo→Pago),
    // igual que valida el servicio, para no ofrecer empates inválidos en la UI.
    private IEnumerable<MovimientoErpDto> Candidatos(MovimientoBancarioDto mov) =>
        pendientes.Where(p => mov.Naturaleza == NaturalezaMovimiento.Abono
            ? p.Origen == "Cobro" : p.Origen == "Pago");

    private async Task Conciliar(MovimientoBancarioDto mov, MovimientoErpDto cand)
    {
        msg = "";
        try
        {
            await BancosSvc.ConciliarAsync(new ConciliarMovimientoRequest(mov.Id, cand.Origen, cand.Id));
            msg = $"Conciliado con {cand.Origen} de ${cand.Monto:N2}."; err = false;
            conciliarMovId = null;
            await CargarDetalle();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private async Task Desconciliar(int movimientoId)
    {
        msg = "";
        try
        {
            await BancosSvc.DesconciliarAsync(movimientoId);
            msg = "Movimiento regresado a pendiente."; err = false;
            await CargarDetalle();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }
}

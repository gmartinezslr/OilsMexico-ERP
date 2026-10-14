using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class EstadoCuentas
{
    [Inject] private IEmailService CorreoSvc { get; set; } = default!;

    private List<EstadoCuentaClienteDto>? _estados;
    private AntiguedadSaldosCxcDto? _antiguedad;
    private bool _cargando, _enviando, _cargandoAntiguedad;
    private string _msg = "", _correoDestino = "";
    private bool _err;
    private List<(int Id, string Nombre)> _clientesPlantilla = [];
    private string? _clienteSel;
    private decimal? _saldoSel;
    private string _tab = "saldos";
    private int? _detalleAbierto;

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

    private async Task VerAntiguedad()
    {
        _tab = "antiguedad";
        if (_antiguedad is not null) return;   // ya cargada (lazy)
        _cargandoAntiguedad = true;
        try
        {
            _antiguedad = await EstadoCuentasSvc.AntiguedadSaldosAsync(SucCtx.SucursalId);

            // Avisos en campana si hay saldos en rango crítico (+31 días).
            var vencidos31 = _antiguedad.TotalRango31_60 + _antiguedad.TotalRango60Mas;
            if (vencidos31 > 0)
                Avisos.Agregar(
                    "Saldos CxC vencidos > 30 días",
                    $"${vencidos31:N2} con más de 30 días sin cobrar en la sucursal.",
                    "aviso");
        }
        catch (Exception ex)
        {
            _msg = "Error al calcular antigüedad: " + ex.Message;
            _err = true;
        }
        finally { _cargandoAntiguedad = false; }
    }

    private void ToggleDetalle(int clienteId) =>
        _detalleAbierto = _detalleAbierto == clienteId ? null : clienteId;

    private async Task EnviarEstadoCuenta(int clienteId)
    {
        _enviando = true;
        try
        {
            var destino = string.IsNullOrWhiteSpace(_correoDestino) ? null : _correoDestino.Trim();
            var r = await CorreoSvc.EnviarEstadoCuentaClienteAsync(clienteId, destino);
            _msg = r.Mensaje; _err = false;
        }
        catch (Exception ex) { _msg = "Error: " + ex.Message; _err = true; }
        finally { _enviando = false; }
    }

    private async Task EnviarRecordatorios()
    {
        _enviando = true;
        try
        {
            var r = await CorreoSvc.EnviarRecordatoriosSaldoAsync();
            _msg = $"Recordatorios enviados: {r.Enviados}; fallidos: {r.Fallidos}; sin correo: {r.SinCorreo}." +
                   (r.Errores.Count > 0 ? " " + string.Join(" | ", r.Errores.Take(3)) : "");
            _err = r.Enviados == 0 && (r.Fallidos > 0 || r.SinCorreo > 0);
        }
        catch (Exception ex) { _msg = "Error: " + ex.Message; _err = true; }
        finally { _enviando = false; }
    }
}

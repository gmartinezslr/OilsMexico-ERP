using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Web.Components.Pages.ERP;

/// <summary>Pantalla de configuración del sistema (solo Admin): parámetros generales del ERP.</summary>
public partial class Configuracion : ComponentBase
{
    private string msg = "";
    private bool err, guardando, esAdmin;
    private int minutos = 5;

    // Política de contraseña (administrable desde esta pantalla).
    private int pwdMin = 20, pwdHist = 3, pwdDias = 90;
    private bool guardandoPwd;

    // Política de bloqueo por intentos de login (antes hardcodeada en AuthService).
    private int bloqIntentos = 3, bloqMinutos = 30, bloqDefinitivo = 2;
    private bool guardandoBloqueo;

    [Inject] private IConfiguracionService Config { get; set; } = default!;
    [Inject] private IAuthService Auth { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        if (esAdmin)
        {
            minutos = await Config.ObtenerMinutosTimeoutAsync();
            await CargarPwd();
            await CargarBloqueo();
        }
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin")) { Nav.NavigateTo("/"); return; }
        esAdmin = true;
        minutos = await Config.ObtenerMinutosTimeoutAsync();
        await CargarPwd();
        await CargarBloqueo();
        StateHasChanged();
    }

    private async Task CargarPwd()
    {
        var cfg = await Auth.ObtenerPasswordConfigAsync();
        pwdMin = cfg.LongitudMinima;
        pwdHist = cfg.Historial;
        pwdDias = cfg.DuracionDias;
    }

    private async Task CargarBloqueo()
    {
        var p = await Config.ObtenerPoliticaBloqueoAsync();
        bloqIntentos = p.MaxIntentos;
        bloqMinutos = p.MinutosBloqueo;
        bloqDefinitivo = p.BloqueosParaDefinitivo;
    }

    private async Task Guardar()
    {
        if (!esAdmin) { msg = "Solo el rol Admin puede modificar la configuración."; err = true; return; }
        guardando = true; msg = ""; StateHasChanged();
        minutos = await Config.GuardarMinutosTimeoutAsync(minutos);
        guardando = false;
        msg = "Configuración guardada. Se aplicará a las sesiones nuevas y en la próxima actividad de las sesiones abiertas.";
        err = false;
        StateHasChanged();
    }

    private async Task GuardarPwd()
    {
        if (!esAdmin) { msg = "Solo el rol Admin puede modificar la política de contraseñas."; err = true; return; }
        guardandoPwd = true; msg = ""; StateHasChanged();
        var r = await Auth.GuardarPasswordConfigAsync(
            new PasswordConfigRequest(pwdMin, pwdHist, pwdDias, true, true, true, true),
            Sesion.Sesion?.UsuarioId ?? 0);
        guardandoPwd = false;
        if (r.Exitoso)
        {
            // Recargar valores normalizados por el servicio.
            await CargarPwd();
        }
        msg = r.Mensaje ?? (r.Exitoso ? "Política guardada." : "No se pudo guardar la política.");
        err = !r.Exitoso;
        StateHasChanged();
    }

    private async Task GuardarBloqueo()
    {
        if (!esAdmin) { msg = "Solo el rol Admin puede modificar la política de bloqueo."; err = true; return; }
        guardandoBloqueo = true; msg = ""; StateHasChanged();
        var p = await Config.GuardarPoliticaBloqueoAsync(bloqIntentos, bloqMinutos, bloqDefinitivo);
        // Recargar valores normalizados por el servicio (rangos acotados).
        await CargarBloqueo();
        guardandoBloqueo = false;
        msg = $"Política de bloqueo guardada ({p.MaxIntentos} intentos → {p.MinutosBloqueo} min; " +
              $"{p.BloqueosParaDefinitivo}.º bloqueo → definitivo).";
        err = false;
        StateHasChanged();
    }
}

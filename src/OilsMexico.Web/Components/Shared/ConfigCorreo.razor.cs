using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Shared;

public partial class ConfigCorreo : ComponentBase
{
    [Parameter] public string Titulo { get; set; } = "Correo saliente (SMTP)";
    [Parameter] public EventCallback<string> AlGuardar { get; set; }

    [Inject] private IEmailService CorreoSvc { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;

    private ConfigCorreoDto cfg = new(false, "", 587, true, "", "", "", "OilsMexico ERP", false);
    private bool esAdmin, guardando, probando;
    private string destinoPrueba = "", msg = "";
    private bool err;

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) return;
        esAdmin = Sesion.Sesion?.Rol == Roles.Admin;
        if (esAdmin) { try { cfg = await CorreoSvc.ObtenerConfigAsync(); } catch { } StateHasChanged(); }
    }

    private void Set<T>(T v, Func<T, ConfigCorreoDto> f) => cfg = f(v);

    private async Task Guardar()
    {
        guardando = true; msg = ""; StateHasChanged();
        try
        {
            cfg = await CorreoSvc.GuardarConfigAsync(cfg with { Puerto = Math.Clamp(cfg.Puerto, 1, 65535) });
            msg = "Correo saliente guardado. El remitente configurado es el origen de cotizaciones, CFDI, estados de cuenta, comunicados y recordatorios.";
            err = false;
            await AlGuardar.InvokeAsync(msg);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false; StateHasChanged();
    }

    private async Task Probar()
    {
        probando = true; msg = ""; StateHasChanged();
        try
        {
            var r = await CorreoSvc.ProbarAsync(string.IsNullOrWhiteSpace(destinoPrueba) ? null : destinoPrueba.Trim());
            msg = r.Mensaje; err = false;
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        probando = false; StateHasChanged();
    }
}

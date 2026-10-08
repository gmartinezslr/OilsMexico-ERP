using Microsoft.AspNetCore.Components;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Web.Components.Pages.ERP;

/// <summary>Pantalla de configuración del sistema (solo Admin): parámetros generales del ERP.</summary>
public partial class Configuracion : ComponentBase
{
    private string msg = "";
    private bool err, guardando, esAdmin;
    private int minutos = 5;

    [Inject] private IConfiguracionService Config { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        if (esAdmin) minutos = await Config.ObtenerMinutosTimeoutAsync();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin")) { Nav.NavigateTo("/"); return; }
        esAdmin = true;
        minutos = await Config.ObtenerMinutosTimeoutAsync();
        StateHasChanged();
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
}

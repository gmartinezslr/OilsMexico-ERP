using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en CorteCaja.razor.
public partial class CorteCaja : ComponentBase
{
    private CorteAbiertoDto? abierto;
    private CortePreviewDto? foto;
    private List<CorteListadoDto> historial = [];
    private decimal fondo, contado;
    private string obs = "", msg = "";
    private bool err, cargando;

    protected override async Task OnInitializedAsync() => await Cargar();

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            abierto = await Caja.AbiertoAsync(SucCtx.SucursalId);
            foto = abierto is null ? null : await Caja.PreviewAsync(abierto.CorteId);
            if (foto is not null) contado = abierto!.FondoInicial + foto.EfectivoSistema;
            historial = await Caja.HistorialAsync(SucCtx.SucursalId, 30);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task Abrir()
    {
        cargando = true;
        try
        {
            abierto = await Caja.AbrirAsync(SucCtx.SucursalId, fondo);
            msg = $"Caja abierta con fondo ${abierto.FondoInicial:N2}."; err = false;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task Refrescar() => await Cargar();

    private async Task Cerrar()
    {
        if (abierto is null) return;
        cargando = true;
        try
        {
            var r = await Caja.CerrarAsync(new CerrarCorteRequest(abierto.CorteId, contado, obs));
            msg = r.Diferencia == 0
                ? $"Corte cuadrado ✓ Ventas ${r.TotalVentasSistema:N2}."
                : $"Corte cerrado con diferencia ${r.Diferencia:N2} (ventas ${r.TotalVentasSistema:N2}).";
            err = r.Diferencia != 0;
            fondo = 0; obs = "";
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }
}

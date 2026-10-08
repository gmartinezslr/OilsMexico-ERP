using Microsoft.AspNetCore.Components;
using OilsMexico.Infrastructure.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en Viscosidad.razor.
public partial class Viscosidad : ComponentBase
{
    private List<ViscosidadProductoDto> todos = [];
    private string texto = "", visc = "", msg = "";
    private bool err, cargando;

    protected override async Task OnInitializedAsync()
    {
        if (Sesion.Autenticado) await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Almacen")) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            // Se carga todo una vez; los filtros de texto/viscosidad se aplican en cliente.
            todos = await Consulta.ViscosidadAsync(SucCtx.SucursalId);
            msg = "";
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private void AlEscribir(ChangeEventArgs e) { texto = e.Value?.ToString() ?? ""; }

    private void SelVisc(string v) => visc = visc == v ? "" : v;

    private IEnumerable<ViscosidadProductoDto> PorTexto =>
        string.IsNullOrWhiteSpace(texto) ? todos :
        todos.Where(p =>
            p.Sku.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)
            || p.Nombre.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)
            || p.Marca.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase)
            || p.Viscosidad.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase));

    private IEnumerable<ViscosidadProductoDto> Filtrados =>
        visc == "" ? PorTexto : PorTexto.Where(p => p.Viscosidad == visc);

    private List<CardInfo> Tarjetas => PorTexto
        .GroupBy(p => p.Viscosidad)
        .OrderBy(g => g.Key)
        .Select(g => new CardInfo(
            g.Key, g.Count(),
            g.Sum(x => x.StockLitros), g.Sum(x => x.ValorStock),
            g.Count(x => x.BajoMinimo)))
        .ToList();

    private sealed record CardInfo(string Viscosidad, int Productos, decimal Litros, decimal Valor, int Bajo);
}

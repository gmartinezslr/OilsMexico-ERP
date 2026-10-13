using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en Gastos.razor.
public partial class Gastos : ComponentBase
{
    private DateTime desde = DateTime.Today.AddDays(-30);
    private DateTime hasta = DateTime.Today;
    private string categoria = "";
    private List<GastoListadoDto> lista = [];
    private List<GastoTotalCategoriaDto> totales = [];
    private decimal TotalPeriodo => lista.Sum(g => g.Importe);

    private DateTime fecha = DateTime.Now;
    private string cat = "Combustible";
    private string concepto = "", forma = "01", comprobante = "", notas = "";
    private decimal importe;
    private bool editando;
    private int editId;
    private string msg = "";
    private bool err, cargando, guardando;

    protected override async Task OnInitializedAsync()
    {
        if (Sesion.Autenticado) await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not (Roles.Admin or Roles.Conta or Roles.Vendedor)) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            lista = await GastosSvc.ListarAsync(SucCtx.SucursalId, desde, hasta, categoria);
            totales = await GastosSvc.TotalesPorCategoriaAsync(SucCtx.SucursalId, desde, hasta);
            msg = "";
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task Guardar()
    {
        guardando = true;
        try
        {
            var req = new CrearGastoRequest(SucCtx.SucursalId, fecha, cat, concepto, importe, forma, comprobante, notas);
            if (editando)
            {
                var r = await GastosSvc.EditarAsync(editId, req);
                msg = $"Gasto #{r.GastoId} actualizado."; err = false;
            }
            else
            {
                var r = await GastosSvc.CrearAsync(req);
                msg = $"Gasto #{r.GastoId} registrado (${r.Importe:N2})."; err = false;
            }
            CancelarEdicion();
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private void EmpezarEditar(GastoListadoDto g)
    {
        editando = true; editId = g.GastoId;
        fecha = g.Fecha.ToLocalTime(); cat = g.Categoria; concepto = g.Concepto;
        importe = g.Importe; forma = g.FormaPagoSat;
        comprobante = g.Comprobante ?? ""; notas = g.Notas ?? "";
        msg = "";
    }

    private void CancelarEdicion()
    {
        editando = false; editId = 0;
        fecha = DateTime.Now; concepto = ""; importe = 0;
        comprobante = ""; notas = "";
    }

    private async Task Eliminar(int id)
    {
        try
        {
            await GastosSvc.EliminarAsync(id);
            msg = $"Gasto #{id} eliminado."; err = false;
            if (editando && editId == id) CancelarEdicion();
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }
}

using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en ComplementosPago.razor.
public partial class ComplementosPago : ComponentBase
{
    private List<VentaPpdPendienteDto> pendientes = [];
    private List<RepListadoDto> reps = [];
    private RepDetalleDto? detalle;
    private Dictionary<string, string> formas = new();
    private int? cobrarId;
    private decimal monto;
    private string forma = "01", referencia = "", msg = "";
    private bool err, cargando;

    protected override async Task OnInitializedAsync()
    {
        formas = Sat.Obtener().FormasPago;
        await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Conta")) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            pendientes = await Rep.PendientesAsync(SucCtx.SucursalId);
            reps = await Rep.ListarAsync(SucCtx.SucursalId, null);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private void EmpezarCobro(VentaPpdPendienteDto p)
    {
        cobrarId = p.FacturaId; monto = p.Saldo; forma = "01"; referencia = "";
    }

    private async Task ConfirmarCobro(VentaPpdPendienteDto p)
    {
        if (monto <= 0) { msg = "El monto debe ser mayor a cero."; err = true; return; }
        cargando = true;
        try
        {
            var r = await Rep.RegistrarCobroAsync(new RegistrarCobroRequest(p.FacturaId, monto, forma, referencia));
            msg = $"Cobro ${r.Monto:N2} amparado en {r.FolioRep} (insoluto ${r.SaldoInsoluto:N2}).";
            err = false; cobrarId = null; detalle = null;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task VerDetalle(int repId)
    {
        if (detalle?.RepId == repId) { detalle = null; return; }
        detalle = await Rep.DetalleAsync(repId);
    }
}

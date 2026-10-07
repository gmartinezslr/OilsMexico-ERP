using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en Facturacion.razor.
public partial class Facturacion : ComponentBase
{
    private List<FacturaListadoDto> lista = [];
    private string estado = "", texto = "", msg = "", motivoCancelacion = "02", folioSustitucion = "";
    private bool err, cargando, cancelando, puedeCancelar;
    private FacturaListadoDto? sel;
    private HistorialDetalleDto? detalle;

    protected override async Task OnInitializedAsync()
    {
        puedeCancelar = Sesion.Sesion?.Rol is "Admin" or "Conta";
        await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        puedeCancelar = Sesion.Sesion?.Rol is "Admin" or "Conta";
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            lista = await Fact.ListarAsync(SucCtx.SucursalId, estado, texto);
            sel = null; detalle = null; cancelando = false;
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task Buscar() => await Cargar();

    private void AlEscribir(ChangeEventArgs e) { texto = e.Value?.ToString() ?? ""; }

    private async Task Expandir(int facturaId)
    {
        if (sel?.FacturaId == facturaId) { sel = null; detalle = null; cancelando = false; return; }
        sel = lista.FirstOrDefault(f => f.FacturaId == facturaId);
        detalle = null; cancelando = false;
        if (sel is null) return;
        detalle = await Ventas.HistorialDetalleAsync(facturaId);
        if (detalle is null) { msg = "Factura no encontrada (o es de otra sucursal)."; err = true; sel = null; }
    }

    private async Task Timbrar(int facturaId)
    {
        cargando = true;
        try
        {
            var r = await Fact.TimbrarAsync(facturaId);
            msg = $"Factura {r.FolioInterno} timbrada. UUID: {r.UuidSat}"; err = false;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private void EmpezarCancelacion() { cancelando = true; motivoCancelacion = "02"; folioSustitucion = ""; }

    private async Task ConfirmarCancelacion(int facturaId)
    {
        if (motivoCancelacion is not ("01" or "02" or "03" or "04"))
        { msg = "Motivo SAT inválido: usa 01, 02, 03 o 04."; err = true; return; }
        if (motivoCancelacion == "01" && !Guid.TryParse(folioSustitucion?.Trim(), out _))
        { msg = "El motivo 01 exige el UUID del CFDI sustituto."; err = true; return; }
        cargando = true;
        try
        {
            var r = await Fact.CancelarAsync(facturaId, motivoCancelacion.Trim(),
                motivoCancelacion == "01" ? folioSustitucion.Trim() : null);
            msg = $"Factura {r.FolioInterno} cancelada ante el PAC (motivo {motivoCancelacion})."; err = false;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private static string ClaseEstado(string e) => e switch
    {
        "Pendiente" => "bg-secondary",
        "Surtida" => "bg-primary",
        "Timbrada" => "bg-success",
        "Entregada" => "bg-info text-dark",
        "Cancelada" => "bg-danger",
        "Devolucion" => "bg-dark",
        _ => "bg-secondary"
    };
}
using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en NotasCredito.razor. Acepta ?origen={facturaId} para precargar el preview.
public partial class NotasCredito : ComponentBase
{
    private List<NotaCreditoListadoDto> lista = [];
    private NotaCreditoPreviewDto? preview;
    private string estado = "", texto = "", msg = "", motivo = "Devolucion";
    private string observaciones = "", motivoCancelacion = "02", folioSustitucionNc = "";
    private bool err, cargando, puedeCancelar;
    private int? cancelandoId, origenQuery;
    private bool _primeraCarga;

    [SupplyParameterFromQuery(Name = "origen")]
    public int? Origen { get; set; }

    protected override async Task OnInitializedAsync()
    {
        puedeCancelar = Sesion.Sesion?.Rol is "Admin" or "Conta";
        // No cargar aquí: OnAfterRenderAsync lo hace tras restaurar sesión.
        // Cargar en ambos solapa dos consultas sobre el mismo DbContext
        // ("A second operation was started on this context instance").
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor" or "Conta")) { Nav.NavigateTo("/"); return; }
        puedeCancelar = Sesion.Sesion?.Rol is "Admin" or "Conta";
        if (Origen is not null && Origen != origenQuery)
        {
            origenQuery = Origen;
            await PrecargarOrigen(Origen.Value);
        }
        if (!_primeraCarga) { _primeraCarga = true; await Cargar(); StateHasChanged(); }
    }

    private async Task Cargar()
    {
        // Guardia de reentrada: no solapar consultas EF.
        if (cargando) return;
        cargando = true;
        try { lista = await Nc.ListarAsync(SucCtx.SucursalId, estado, texto); }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task Buscar() => await Cargar();

    private void AlEscribir(ChangeEventArgs e) { texto = e.Value?.ToString() ?? ""; }

    private async Task PrecargarOrigen(int facturaId)
    {
        try
        {
            preview = await Nc.PreviewAsync(facturaId);
            motivo = "Devolucion"; observaciones = "";
            msg = $"NC preparada desde {preview.FolioOrigen} por ${preview.Total:N2}."; err = false;
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private void CerrarPreview() { preview = null; }

    private async Task CrearTimbrar()
    {
        if (preview is null) return;
        cargando = true;
        try
        {
            var r = await Nc.CrearYTimbrarAsync(new CrearNotaCreditoRequest(preview.FacturaId, motivo, observaciones));
            msg = $"NC {r.FolioInterno} timbrada. UUID: {r.UuidSat} (stock repuesto)."; err = false;
            preview = null;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private void EmpezarCancelar(int id) { cancelandoId = id; motivoCancelacion = "02"; folioSustitucionNc = ""; }

    private async Task ConfirmarCancelar(int id)
    {
        if (motivoCancelacion is not ("01" or "02" or "03" or "04"))
        { msg = "Motivo SAT inválido: usa 01, 02, 03 o 04."; err = true; return; }
        if (motivoCancelacion == "01" && !Guid.TryParse(folioSustitucionNc?.Trim(), out _))
        { msg = "El motivo 01 exige el UUID del CFDI sustituto."; err = true; return; }
        cargando = true;
        try
        {
            var r = await Nc.CancelarAsync(id, motivoCancelacion.Trim(),
                motivoCancelacion == "01" ? folioSustitucionNc.Trim() : null);
            msg = $"NC {r.FolioInterno} cancelada ante el PAC (motivo {motivoCancelacion})."; err = false;
            cancelandoId = null;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }
}

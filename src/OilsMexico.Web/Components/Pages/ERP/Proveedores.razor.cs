using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Proveedores : ComponentBase
{
    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAutorizacionService Autorizacion { get; set; } = default!;
    private string filtro = "", msg = "";
    private bool err, guardando;
    private int paso = 1;
    // DIP: el componente no conoce ErpDbContext; el padrón vive en IProveedoresService.
    private List<ProveedorCatalogoDto> lista = [];
    private ProveedorCatalogoDto edit = new();
    // Resultado de la última validación de RFC en el campo (feedback en vivo).
    private string rfcInfo = "";
    private bool rfcErr;

    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAuthService Auth { get; set; } = default!;

    [Microsoft.AspNetCore.Components.Inject]
    private IProveedoresService ProveedoresSvc { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("proveedores"))) { Nav.NavigateTo("/"); return; }
        StateHasChanged();
    }

    private async Task Cargar()
    {
        lista = await ProveedoresSvc.ListarAsync(filtro);
    }

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }
    private void Nuevo() { edit = new ProveedorCatalogoDto(); rfcInfo = ""; rfcErr = false; paso = 1; msg = ""; }

    private void IrPaso(int n) { paso = Math.Clamp(n, 1, 3); msg = ""; }

    private void Siguiente()
    {
        msg = "";
        if (paso == 1 && (string.IsNullOrWhiteSpace(edit.Nombre) || string.IsNullOrWhiteSpace(edit.Rfc)))
        { msg = "Paso 1: captura nombre y RFC."; err = true; return; }
        err = false;
        paso = Math.Min(3, paso + 1);
    }

    private void Anterior() { paso = Math.Max(1, paso - 1); msg = ""; }

    /// <summary>Valida el RFC contra el validador oficial al salir del campo (feedback en vivo).</summary>
    private async Task ValidarRfcCampo()
    {
        if (string.IsNullOrWhiteSpace(edit.Rfc)) { rfcInfo = ""; return; }
        var r = await Auth.ValidarRfcAsync(edit.Rfc);
        rfcErr = !r.Valido;
        rfcInfo = r.Valido
            ? $"✓ RFC válido — {r.Tipo} — fecha {r.Dia:D2}/{r.Mes:D2}/{r.Año}"
            : $"✗ {r.Mensaje}";
    }

    private async Task Editar(int id)
    {
        var p = await ProveedoresSvc.ObtenerAsync(id);
        if (p is null) return;
        rfcInfo = ""; rfcErr = false;
        paso = 1; msg = "";
        edit = p with { };
    }

    private async Task Guardar()
    {
        msg = "";
        if (string.IsNullOrWhiteSpace(edit.Nombre) || string.IsNullOrWhiteSpace(edit.Rfc))
        { msg = "Nombre y RFC son obligatorios."; err = true; return; }
        edit.Rfc = edit.Rfc.Trim().ToUpperInvariant();
        // Validación de RFC: estructura + semántica (fecha) + dígito verificador oficial del SAT.
        var rfcRes = await Auth.ValidarRfcAsync(edit.Rfc);
        if (!rfcRes.Valido)
        { msg = rfcRes.Mensaje ?? "RFC no válido."; err = true; return; }
        // No se opera con proveedores cuya situación fiscal lo impide, aunque el RFC sea válido.
        if (edit.SituacionFiscal is SituacionFiscal.Cancelada or SituacionFiscal.Baja)
        { msg = "Situación fiscal Cancelada/Baja: no se puede operar con este proveedor."; err = true; return; }
        edit.CodigoPostal = (edit.CodigoPostal ?? "").Trim();
        if (edit.CodigoPostal.Length != 5 || !edit.CodigoPostal.All(char.IsDigit))
        { msg = "Código postal debe tener 5 dígitos (usa el buscador SEPOMEX)."; err = true; return; }
        if (string.IsNullOrWhiteSpace(edit.Colonia))
        { msg = "Selecciona la colonia del catálogo SEPOMEX."; err = true; return; }
        guardando = true;
        try
        {
            var (ok, mensaje, _) = await ProveedoresSvc.GuardarAsync(edit);
            msg = mensaje; err = !ok;
            if (ok) await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Desactivar()
    {
        var (ok, mensaje) = await ProveedoresSvc.DesactivarAsync(edit.Id);
        msg = ok ? "Proveedor desactivado." : mensaje; err = !ok;
        if (!ok) return;
        Nuevo();
        await Cargar();
    }
}

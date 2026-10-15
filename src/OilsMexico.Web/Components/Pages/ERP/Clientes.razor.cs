using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Clientes : ComponentBase
{
    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAutorizacionService Autorizacion { get; set; } = default!;
    private string filtro = "", msg = "";
    private string vendedorSel = "";
    private bool err, guardando;
    private int paso = 1;
    // DIP: el componente no conoce ErpDbContext; el padrón vive en IClientesService
    // y los catálogos de apoyo en ICatalogosService.
    private List<ClienteCatalogoDto> lista = [];
    private ClienteCatalogoDto edit = new();
    private List<(int Id, string Nombre)> vendedores = [];
    private CatalogosSatDto catalogos = new([], [], [], []);
    // Resultado de la última validación de RFC en el campo (feedback en vivo).
    private string rfcInfo = "";
    private bool rfcErr;

    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAuthService Auth { get; set; } = default!;

    [Microsoft.AspNetCore.Components.Inject]
    private IClientesService ClientesSvc { get; set; } = default!;

    [Microsoft.AspNetCore.Components.Inject]
    private ICatalogosService Catalogos { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        catalogos = Sat.Obtener();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("clientes"))) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        if (vendedores.Count == 0)
        {
            // Catálogo de vendedores (rol que puede recibir comisiones).
            vendedores = (await Catalogos.ListarVendedoresAsync())
                .Select(v => (v.Id, v.Nombre)).ToList();
        }
        lista = await ClientesSvc.ListarAsync(filtro);
        if (edit.Id == 0 && lista.Count > 0) Seleccionar(lista[0]);
    }

    private void Seleccionar(ClienteCatalogoDto c)
    {
        edit = c with { };
        vendedorSel = c.VendedorId?.ToString() ?? "";
        rfcInfo = ""; rfcErr = false;
        paso = 1; msg = "";
    }

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

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }

    private void Nuevo()
    {
        edit = new ClienteCatalogoDto
        {
            CodigoPostal = "06600",
            Contactos =
            [
                new() { EsPrincipal = true },
                new() { EsRespaldo = true }
            ],
            Direcciones = [new() { Tipo = "Fiscal", EsPredeterminada = true, CodigoPostal = "06600" }]
        };
        vendedorSel = ""; paso = 1; msg = "";
    }

    private void IrPaso(int n) { paso = Math.Clamp(n, 1, 3); msg = ""; }

    private void Siguiente()
    {
        // El wizard guía pero no bloquea datos ya capturados: valida el paso actual.
        msg = "";
        if (paso == 1 && (string.IsNullOrWhiteSpace(edit.Nombre) || string.IsNullOrWhiteSpace(edit.Rfc)))
        { msg = "Paso 1: captura Nombre y RFC antes de continuar."; err = true; return; }
        if (paso == 2 && string.IsNullOrWhiteSpace(edit.RegimenFiscal))
        { msg = "Paso 2: elige el régimen fiscal."; err = true; return; }
        if (paso == 3 && !ValidarDirecciones()) { err = true; return; }
        err = false;
        paso = Math.Min(4, paso + 1);
    }

    private void Anterior() { paso = Math.Max(1, paso - 1); msg = ""; }

    private async Task Editar(int id)
    {
        var c = await ClientesSvc.ObtenerAsync(id);
        if (c is not null) Seleccionar(c);
    }

    private async Task Guardar()
    {
        msg = "";
        if (string.IsNullOrWhiteSpace(edit.Nombre) || string.IsNullOrWhiteSpace(edit.Rfc))
        { msg = "Nombre y RFC son obligatorios."; err = true; return; }
        if (!ValidarDirecciones()) { err = true; return; }
        if (!string.IsNullOrWhiteSpace(vendedorSel)
            && (!int.TryParse(vendedorSel, out var vid) || !vendedores.Any(v => v.Id == vid)))
        { msg = "Selecciona un vendedor válido de la lista."; err = true; return; }
        edit.VendedorId = string.IsNullOrWhiteSpace(vendedorSel) ? null : int.Parse(vendedorSel);
        edit.Rfc = edit.Rfc.Trim().ToUpperInvariant();
        // Validación de RFC: estructura + semántica (fecha) + dígito verificador oficial del SAT.
        var rfcRes = await Auth.ValidarRfcAsync(edit.Rfc);
        if (!rfcRes.Valido)
        { msg = rfcRes.Mensaje ?? "RFC no válido."; err = true; return; }
        // No se opera con clientes cuya situación fiscal lo impide, aunque el RFC sea estructuralmente válido.
        if (edit.SituacionFiscal is SituacionFiscal.Cancelada or SituacionFiscal.Baja)
        { msg = "Situación fiscal Cancelada/Baja: no se puede facturar ni operar con este cliente."; err = true; return; }
        edit.CodigoPostal = (edit.CodigoPostal ?? "").Trim();
        if (edit.CodigoPostal.Length != 5 || !edit.CodigoPostal.All(char.IsDigit))
        { msg = "Código postal debe tener 5 dígitos (usa el buscador SEPOMEX)."; err = true; return; }
        if (string.IsNullOrWhiteSpace(edit.Colonia))
        { msg = "Selecciona la colonia del catálogo SEPOMEX."; err = true; return; }
        guardando = true;
        try
        {
            var (ok, mensaje, id) = await ClientesSvc.GuardarAsync(edit);
            msg = mensaje; err = !ok;
            if (ok)
            {
                Avisos.Agregar($"Cliente guardado: {edit.Nombre}", $"RFC {edit.Rfc} · {edit.RegimenFiscal}", "exito");
                await Cargar();
                Seleccionar((await ClientesSvc.ObtenerAsync(id))!);
            }
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Desactivar()
    {
        var (ok, mensaje) = await ClientesSvc.DesactivarAsync(edit.Id);
        msg = ok ? "Cliente desactivado." : mensaje; err = !ok;
        if (!ok) return;
        Nuevo();
        await Cargar();
    }

    private bool ValidarDirecciones()
    {
        var capturadas = edit.Direcciones.Where(d =>
            !string.IsNullOrWhiteSpace(d.Calle) || !string.IsNullOrWhiteSpace(d.Colonia)
            || !string.IsNullOrWhiteSpace(d.CodigoPostal) || !string.IsNullOrWhiteSpace(d.Municipio)).ToList();
        if (capturadas.Count == 0)
        { msg = "Registra al menos una dirección."; return false; }
        if (capturadas.Any(d => string.IsNullOrWhiteSpace(d.Colonia)
            || string.IsNullOrWhiteSpace(d.CodigoPostal) || d.CodigoPostal.Length != 5
            || !d.CodigoPostal.All(char.IsDigit)))
        { msg = "Cada dirección iniciada debe tener CP de 5 dígitos y colonia de SEPOMEX."; return false; }
        if (!capturadas.Any(d => d.EsPredeterminada))
        { msg = "Marca una dirección predeterminada para al menos un uso."; return false; }
        return true;
    }
}

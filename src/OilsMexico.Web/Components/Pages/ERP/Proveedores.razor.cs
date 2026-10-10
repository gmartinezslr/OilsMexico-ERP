using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Proveedores : ComponentBase
{
    private string filtro = "", msg = "";
    private bool err, guardando;
    private List<Proveedor> lista = [];
    private Proveedor edit = new();
    // Resultado de la última validación de RFC en el campo (feedback en vivo).
    private string rfcInfo = "";
    private bool rfcErr;

    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAuthService Auth { get; set; } = default!;

    protected override async Task OnInitializedAsync() { if (Sesion.Autenticado) await Cargar(); }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor")) { Nav.NavigateTo("/"); return; }
        StateHasChanged();
    }

    private async Task Cargar()
    {
        var q = Db.Proveedores.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(p => p.Nombre.ToLower().Contains(f) || p.Rfc.ToLower().Contains(f));
        }
        lista = await q.Take(200).ToListAsync();
    }

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }
    private void Nuevo() { edit = new Proveedor(); rfcInfo = ""; rfcErr = false; }

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
        var p = await Db.Proveedores.FindAsync(id);
        if (p is null) return;
        rfcInfo = ""; rfcErr = false;
        edit = new Proveedor
        {
            Id = p.Id, Nombre = p.Nombre, Rfc = p.Rfc, Telefono = p.Telefono, Email = p.Email,
            Calle = p.Calle, NumeroExterior = p.NumeroExterior, NumeroInterior = p.NumeroInterior,
            Colonia = p.Colonia, CodigoPostal = p.CodigoPostal, Municipio = p.Municipio,
            Estado = p.Estado, Ciudad = p.Ciudad, Pais = p.Pais, Direccion = p.Direccion, Activo = p.Activo,
            SituacionFiscal = p.SituacionFiscal
        };
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
            edit.Direccion = $"{edit.Calle} {edit.NumeroExterior} {edit.NumeroInterior}, {edit.Colonia}, CP {edit.CodigoPostal}, {edit.Municipio}, {edit.Estado}".Trim(' ', ',');
            if (edit.Id == 0) Db.Proveedores.Add(edit);
            else Db.Proveedores.Update(edit);
            await Db.SaveChangesAsync();
            msg = $"Proveedor '{edit.Nombre}' guardado."; err = false;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Desactivar()
    {
        edit.Activo = false;
        Db.Proveedores.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Proveedor desactivado."; err = false;
        Nuevo();
        await Cargar();
    }
}

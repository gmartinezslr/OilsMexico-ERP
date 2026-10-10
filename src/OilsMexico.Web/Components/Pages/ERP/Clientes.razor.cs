using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Clientes : ComponentBase
{
    private string filtro = "", msg = "";
    private string vendedorSel = "";
    private bool err, guardando;
    private List<Cliente> lista = [];
    private Cliente edit = new();
    private List<(int Id, string Nombre)> vendedores = [];
    private CatalogosSatDto catalogos = new([], [], [], []);
    // Resultado de la última validación de RFC en el campo (feedback en vivo).
    private string rfcInfo = "";
    private bool rfcErr;

    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAuthService Auth { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        catalogos = Sat.Obtener();
        await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor")) { Nav.NavigateTo("/"); return; }
        StateHasChanged();
    }

    private async Task Cargar()
    {
        if (vendedores.Count == 0)
        {
            // Catálogo de vendedores (rol que puede recibir comisiones).
            vendedores = await Db.Usuarios.AsNoTracking()
                .Where(u => u.Activo && (u.Rol == "Vendedor" || u.Rol == "Admin"))
                .OrderBy(u => u.Nombre)
                .Select(u => new ValueTuple<int, string>(u.Id, u.Nombre))
                .ToListAsync();
        }
        var q = Db.Clientes.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Nombre).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(c => c.Nombre.ToLower().Contains(f) || c.Rfc.ToLower().Contains(f));
        }
        lista = await q.Take(200).ToListAsync();
        if (edit.Id == 0 && lista.Count > 0) Seleccionar(lista[0]);
    }

    private void Seleccionar(Cliente c)
    {
        edit = Clonar(c);
        vendedorSel = c.VendedorId?.ToString() ?? "";
        rfcInfo = ""; rfcErr = false;
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

    private void Nuevo() { edit = new Cliente { CodigoPostal = "06600" }; vendedorSel = ""; }

    private async Task Editar(int id)
    {
        var c = await Db.Clientes.FindAsync(id);
        if (c is not null) Seleccionar(c);
    }

    private async Task Guardar()
    {
        msg = "";
        if (string.IsNullOrWhiteSpace(edit.Nombre) || string.IsNullOrWhiteSpace(edit.Rfc))
        { msg = "Nombre y RFC son obligatorios."; err = true; return; }
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
            edit.Direccion = $"{edit.Calle} {edit.NumeroExterior} {edit.NumeroInterior}, {edit.Colonia}, CP {edit.CodigoPostal}, {edit.Municipio}, {edit.Estado}".Trim(' ', ',');
            if (edit.Id == 0) Db.Clientes.Add(edit);
            else Db.Clientes.Update(edit);
            await Db.SaveChangesAsync();
            msg = $"Cliente '{edit.Nombre}' guardado."; err = false;
            var id = edit.Id;
            await Cargar();
            Seleccionar((await Db.Clientes.FindAsync(id))!);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Desactivar()
    {
        edit.Activo = false;
        Db.Clientes.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Cliente desactivado."; err = false;
        Nuevo();
        await Cargar();
    }

    private static Cliente Clonar(Cliente c) => new()
    {
        Id = c.Id, Nombre = c.Nombre, Rfc = c.Rfc, Telefono = c.Telefono, Email = c.Email,
        Direccion = c.Direccion, TipoPrecio = c.TipoPrecio, RegimenFiscal = c.RegimenFiscal,
        CodigoPostal = c.CodigoPostal, Calle = c.Calle, NumeroExterior = c.NumeroExterior,
        NumeroInterior = c.NumeroInterior, Colonia = c.Colonia, Municipio = c.Municipio,
        Estado = c.Estado, Ciudad = c.Ciudad, Pais = c.Pais, Activo = c.Activo,
        VendedorId = c.VendedorId, SituacionFiscal = c.SituacionFiscal
    };
}

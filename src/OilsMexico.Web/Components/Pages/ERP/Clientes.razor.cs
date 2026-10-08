using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Clientes : ComponentBase
{
    private string filtro = "", msg = "";
    private bool err, guardando;
    private List<Cliente> lista = [];
    private Cliente edit = new();
    private CatalogosSatDto catalogos = new([], [], [], []);

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
        var q = Db.Clientes.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Nombre).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(c => c.Nombre.ToLower().Contains(f) || c.Rfc.ToLower().Contains(f));
        }
        lista = await q.Take(200).ToListAsync();
        if (edit.Id == 0 && lista.Count > 0) edit = Clonar(lista[0]);
    }

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }

    private void Nuevo() => edit = new Cliente { CodigoPostal = "06600" };

    private async Task Editar(int id)
    {
        var c = await Db.Clientes.FindAsync(id);
        if (c is not null) edit = Clonar(c);
    }

    private async Task Guardar()
    {
        msg = "";
        if (string.IsNullOrWhiteSpace(edit.Nombre) || string.IsNullOrWhiteSpace(edit.Rfc))
        { msg = "Nombre y RFC son obligatorios."; err = true; return; }
        edit.Rfc = edit.Rfc.Trim().ToUpperInvariant();
        if (edit.Rfc.Length < 12 || edit.Rfc.Length > 13)
        { msg = "RFC debe tener 12 (moral) o 13 (física) caracteres."; err = true; return; }
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
            edit = Clonar((await Db.Clientes.FindAsync(id))!);
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
        Estado = c.Estado, Ciudad = c.Ciudad, Pais = c.Pais, Activo = c.Activo
    };
}

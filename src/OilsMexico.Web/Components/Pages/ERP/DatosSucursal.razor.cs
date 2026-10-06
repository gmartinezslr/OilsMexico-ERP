using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class DatosSucursal : ComponentBase
{
    private string msg = "";
    private bool err, guardando, esAdmin;
    private List<Sucursal> lista = [];
    private Sucursal edit = new();
    private CatalogosSatDto catalogos = new([], [], [], []);

    protected override async Task OnInitializedAsync()
    {
        catalogos = Sat.Obtener();
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        StateHasChanged();
    }

    private async Task Cargar()
    {
        lista = await Db.Sucursales.AsNoTracking().OrderBy(s => s.CodigoSucursal).ToListAsync();
        if (edit.Id == 0 && lista.Count > 0)
        {
            var mia = lista.FirstOrDefault(s => s.Id == SucCtx.SucursalId);
            edit = Clonar(mia ?? lista[0]);
        }
    }

    private async Task Editar(int id)
    {
        var s = await Db.Sucursales.FindAsync(id);
        if (s is not null) edit = Clonar(s);
        msg = "";
    }

    private async Task Guardar()
    {
        msg = "";
        if (!esAdmin) { msg = "Solo el rol Admin puede guardar cambios."; err = true; return; }
        if (string.IsNullOrWhiteSpace(edit.Nombre)) { msg = "El nombre es obligatorio."; err = true; return; }

        edit.CodigoSucursal = edit.CodigoSucursal.Trim().ToUpperInvariant();
        if (edit.CodigoSucursal.Length == 0 || edit.CodigoSucursal.Length > 10)
        { msg = "Código de sucursal obligatorio (máx. 10 caracteres)."; err = true; return; }

        edit.RfcEmisor = edit.RfcEmisor.Trim().ToUpperInvariant();
        if (edit.RfcEmisor.Length < 12 || edit.RfcEmisor.Length > 13)
        { msg = "RFC del emisor debe tener 12 (moral) o 13 (física) caracteres."; err = true; return; }

        if (string.IsNullOrWhiteSpace(edit.RazonSocial)) edit.RazonSocial = edit.Nombre;
        edit.CodigoPostal = (edit.CodigoPostal ?? "").Trim();
        if (edit.CodigoPostal.Length != 5 || !edit.CodigoPostal.All(char.IsDigit))
        { msg = "Código postal del domicilio fiscal debe tener 5 dígitos (usa el buscador SEPOMEX)."; err = true; return; }
        if (string.IsNullOrWhiteSpace(edit.Colonia))
        { msg = "Selecciona la colonia del catálogo SEPOMEX."; err = true; return; }

        var codigoDuplicado = await Db.Sucursales.AnyAsync(
            s => s.CodigoSucursal == edit.CodigoSucursal && s.Id != edit.Id);
        if (codigoDuplicado) { msg = $"Ya existe otra sucursal con el código '{edit.CodigoSucursal}'."; err = true; return; }

        guardando = true;
        try
        {
            edit.Direccion = $"{edit.Calle} {edit.NumeroExterior} {edit.NumeroInterior}, {edit.Colonia}, CP {edit.CodigoPostal}, {edit.Municipio}, {edit.Estado}".Trim(' ', ',');
            if (edit.Id == 0) Db.Sucursales.Add(edit);
            else Db.Sucursales.Update(edit);
            await Db.SaveChangesAsync();
            msg = $"Sucursal '{edit.Nombre}' guardada."; err = false;
            var id = edit.Id;
            await Cargar();
            edit = Clonar((await Db.Sucursales.FindAsync(id))!);
        }
        catch (DbUpdateException) { msg = "No se pudo guardar: ¿código de sucursal duplicado?"; err = true; }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private static Sucursal Clonar(Sucursal s) => new()
    {
        Id = s.Id, Nombre = s.Nombre, CodigoSucursal = s.CodigoSucursal,
        Direccion = s.Direccion, RfcEmisor = s.RfcEmisor, Activa = s.Activa,
        RazonSocial = s.RazonSocial, RegimenFiscal = s.RegimenFiscal,
        CodigoPostal = s.CodigoPostal, Calle = s.Calle, NumeroExterior = s.NumeroExterior,
        NumeroInterior = s.NumeroInterior, Colonia = s.Colonia, Municipio = s.Municipio,
        Estado = s.Estado, Ciudad = s.Ciudad, Pais = s.Pais,
        Telefono = s.Telefono, Email = s.Email
    };
}
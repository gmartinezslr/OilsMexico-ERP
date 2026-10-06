using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Usuarios : ComponentBase
{
    private static readonly string[] RolesValidos = ["Admin", "Vendedor", "Almacen", "Conta"];

    private string filtro = "", msg = "", pin1 = "", pin2 = "";
    private bool err, guardando, esAdmin, soloActivos = true;
    private int miId;
    private List<UsuarioRow> lista = [];
    private Usuario edit = new() { Rol = "Vendedor", Activo = true };
    private List<SucRow> sucursales = [];

    private sealed record UsuarioRow(int Id, string Nombre, string Rol, string Sucursal, bool Activo);
    private sealed record SucRow(int Id, string Nombre, string Codigo);

    protected override async Task OnInitializedAsync()
    {
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        miId = Sesion.Sesion?.UsuarioId ?? 0;
        sucursales = await Db.Sucursales.AsNoTracking().OrderBy(s => s.CodigoSucursal)
            .Select(s => new SucRow(s.Id, s.Nombre, s.CodigoSucursal)).ToListAsync();
        if (sucursales.Count > 0) edit.SucursalId = sucursales[0].Id;
        await Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        miId = Sesion.Sesion?.UsuarioId ?? 0;
        StateHasChanged();
    }

    private async Task Cargar()
    {
        var q = from u in Db.Usuarios.AsNoTracking()
                join s in Db.Sucursales.AsNoTracking() on u.SucursalId equals s.Id into sj
                from s in sj.DefaultIfEmpty()
                orderby u.Nombre
                select new { u, Suc = s != null ? s.Nombre : "-" };
        if (soloActivos) q = q.Where(x => x.u.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(x => x.u.Nombre.ToLower().Contains(f) || x.u.Rol.ToLower().Contains(f)
                || x.Suc.ToLower().Contains(f));
        }
        lista = await q.Take(200).Select(x =>
            new UsuarioRow(x.u.Id, x.u.Nombre, x.u.Rol, x.Suc, x.u.Activo)).ToListAsync();
        if (edit.Id == 0 && lista.Count > 0) await Editar(lista[0].Id);
    }

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }

    private void Nuevo()
    {
        if (!esAdmin) return;
        edit = new Usuario { Rol = "Vendedor", Activo = true, SucursalId = sucursales.Count > 0 ? sucursales[0].Id : 0 };
        pin1 = pin2 = "";
        msg = "";
    }

    private async Task Editar(int id)
    {
        var u = await Db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return;
        edit = new Usuario { Id = u.Id, Nombre = u.Nombre, PinHash = u.PinHash, Rol = u.Rol, SucursalId = u.SucursalId, Activo = u.Activo };
        pin1 = pin2 = "";
        msg = "";
    }

    private async Task Guardar()
    {
        msg = "";
        if (!esAdmin) { msg = "Solo el rol Admin puede guardar cambios."; err = true; return; }
        edit.Nombre = (edit.Nombre ?? "").Trim();
        if (string.IsNullOrWhiteSpace(edit.Nombre))
        { msg = "El nombre es obligatorio."; err = true; return; }
        if (!RolesValidos.Contains(edit.Rol))
        { msg = "Rol invalido."; err = true; return; }
        if (!await Db.Sucursales.AnyAsync(s => s.Id == edit.SucursalId))
        { msg = "Selecciona una sucursal valida."; err = true; return; }

        var quierePin = !string.IsNullOrEmpty(pin1) || !string.IsNullOrEmpty(pin2);
        if (edit.Id == 0 && !quierePin)
        { msg = "El PIN es obligatorio para un usuario nuevo."; err = true; return; }
        if (quierePin)
        {
            var p1 = (pin1 ?? "").Trim();
            var p2 = (pin2 ?? "").Trim();
            if (p1.Length < 4 || p1.Length > 8 || !p1.All(char.IsDigit))
            { msg = "El PIN debe tener de 4 a 8 digitos."; err = true; return; }
            if (p1 != p2) { msg = "Los PIN no coinciden."; err = true; return; }
            edit.PinHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p1)));
        }
        if (edit.Id == 0 && string.IsNullOrEmpty(edit.PinHash))
        { msg = "El PIN es obligatorio."; err = true; return; }

        if (edit.Id == miId && !edit.Activo)
        { msg = "No puedes desactivar tu propio usuario."; err = true; return; }
        if (edit.Id == miId && edit.Rol != "Admin")
        { msg = "No puedes quitarte el rol Admin a ti mismo."; err = true; return; }

        guardando = true;
        try
        {
            if (edit.Id == 0) Db.Usuarios.Add(edit);
            else Db.Usuarios.Update(edit);
            await Db.SaveChangesAsync();
            msg = $"Usuario '{edit.Nombre}' guardado."; err = false;
            pin1 = pin2 = "";
            var id = edit.Id;
            await Cargar();
            await Editar(id);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Desactivar()
    {
        if (!esAdmin) return;
        if (edit.Id == miId) { msg = "No puedes desactivar tu propio usuario."; err = true; return; }
        edit.Activo = false;
        Db.Usuarios.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Usuario desactivado (ya no puede entrar con PIN)."; err = false;
        await Cargar();
        await Editar(edit.Id);
    }

    private async Task Reactivar()
    {
        if (!esAdmin) return;
        edit.Activo = true;
        Db.Usuarios.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Usuario reactivado."; err = false;
        await Cargar();
        await Editar(edit.Id);
    }
}

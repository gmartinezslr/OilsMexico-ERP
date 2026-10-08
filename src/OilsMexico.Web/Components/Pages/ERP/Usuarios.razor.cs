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
    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAuthService Auth { get; set; } = default!;

    private sealed record UsuarioRow(int Id, string Nombre, string Rol, string Sucursal, bool Activo, bool BloqueadoDefinitivo, bool BloqueadoTemporal);
    private sealed record SucRow(int Id, string Nombre, string Codigo);

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        if (!esAdmin) { Nav.NavigateTo("/"); return; }
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
        if (Sesion.Sesion?.Rol != "Admin") { Nav.NavigateTo("/"); return; }
        esAdmin = Sesion.Sesion?.Rol == "Admin";
        miId = Sesion.Sesion?.UsuarioId ?? 0;
        StateHasChanged();
    }

    private async Task Cargar()
    {
        if (!esAdmin) { lista = []; return; }
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
            new UsuarioRow(x.u.Id, x.u.Nombre, x.u.Rol, x.Suc, x.u.Activo, x.u.BloqueadoDefinitivamente, x.u.BloqueadoHastaUtc != null && x.u.BloqueadoHastaUtc > DateTime.UtcNow)).ToListAsync();
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
        edit = u;
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
        edit.Correo = string.IsNullOrWhiteSpace(edit.Correo) ? null : edit.Correo.Trim();
        if (edit.Correo is not null && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(edit.Correo))
        { msg = "El correo de acceso no es válido."; err = true; return; }
        if (edit.Id == 0 && edit.Correo is null)
        { msg = "El correo es obligatorio para un usuario nuevo."; err = true; return; }
        if (edit.Correo is not null && await Db.Usuarios.AnyAsync(x => x.Id != edit.Id && x.Correo != null && x.Correo.ToLower() == edit.Correo.ToLower()))
        { msg = "Ese correo ya está asignado a otro usuario."; err = true; return; }
        if (!await Db.Sucursales.AnyAsync(s => s.Id == edit.SucursalId))
        { msg = "Selecciona una sucursal valida."; err = true; return; }

        var quierePassword = !string.IsNullOrEmpty(pin1) || !string.IsNullOrEmpty(pin2);
        if (edit.Id == 0 && !quierePassword)
        { msg = "La contraseña es obligatoria para un usuario nuevo."; err = true; return; }
        if (quierePassword)
        {
            var p1 = (pin1 ?? "").Trim();
            var p2 = (pin2 ?? "").Trim();
            if (p1.Length < 8 || p1.Length > 100)
            { msg = "La contraseña debe tener de 8 a 100 caracteres."; err = true; return; }
            if (p1 != p2) { msg = "Las contraseñas no coinciden."; err = true; return; }
            edit.PasswordHash = OilsMexico.Infrastructure.Services.AuthService.Hash(p1);
            edit.PinHash = string.Empty;
            edit.SesionToken = null; // Revoca sesiones abiertas al cambiar la contraseña.
        }
        if (edit.Id == 0 && string.IsNullOrEmpty(edit.PasswordHash))
        { msg = "La contraseña es obligatoria."; err = true; return; }

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
        edit.SesionToken = null; // Revoca las sesiones abiertas del usuario desactivado.
        Db.Usuarios.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Usuario desactivado (ya no puede iniciar sesión)."; err = false;
        await Cargar();
        await Editar(edit.Id);
    }

    private async Task Desbloquear()
    {
        if (!esAdmin || edit.Id == 0) return;
        var id = edit.Id;
        if (!await Auth.DesbloquearUsuarioAsync(id)) { msg = "No se encontró el usuario."; err = true; return; }
        msg = "Acceso desbloqueado. El contador de bloqueos fue reiniciado."; err = false;
        await Editar(id);
        await Cargar();
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

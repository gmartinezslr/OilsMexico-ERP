using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Web.Services;


/// <summary>Sesión persistente por navegador: sobrevive navegaciones y recargas del circuito.</summary>
public sealed class SesionActual(ProtectedSessionStorage storage, ISucursalContext ctx, IServiceScopeFactory scopes) : ISesionActual
{
    private const string Clave = "oilsmexico.sesion";
    public SesionDto? Sesion { get; private set; }
    public bool Autenticado => Sesion is not null;

    public async Task IniciarAsync(SesionDto sesion)
    {
        Sesion = sesion;
        ctx.Establecer(sesion.SucursalId, sesion.UsuarioId, sesion.Rol);
        await storage.SetAsync(Clave, sesion);
    }

    public async Task CerrarAsync(bool revocarTokenEnBd = false)
    {
        var usuarioId = Sesion?.UsuarioId;
        Sesion = null;
        // Revoca el token en BD (UPDATE directo) para invalidar también las demás sesiones abiertas
        // del mismo usuario: en su próxima revalidación RestaurarAsync las cerrará.
        if (revocarTokenEnBd && usuarioId is int id && id > 0)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                await db.Usuarios.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SesionToken, (string?)null));
            }
            catch { }
        }
        try { await storage.DeleteAsync(Clave); } catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Restaura la sesión del navegador revalidándola contra la base de datos:
    /// el usuario debe seguir activo, sin bloqueo vigente y con el mismo token de sesión
    /// (se revoca al desbloquear, desactivar o cambiar la contraseña). Rol y sucursal se
    /// refrescan desde BD para que los permisos vigentes sean los que valgan.
    /// </summary>
    public async Task<bool> RestaurarAsync()
    {
        try
        {
            var r = await storage.GetAsync<SesionDto>(Clave);
            if (r.Success && r.Value is not null)
            {
                var s = r.Value;
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                var u = await db.Usuarios.AsNoTracking().Include(x => x.Sucursal)
                    .FirstOrDefaultAsync(x => x.Id == s.UsuarioId);
                if (!Valida(u, s.SesionToken))
                {
                    await CerrarAsync();
                    return false;
                }
                Sesion = new SesionDto(u!.Id, u.Nombre, u.Rol, u.SucursalId, u.Sucursal?.Nombre ?? "", u.SesionToken);
                ctx.Establecer(Sesion.SucursalId, Sesion.UsuarioId, Sesion.Rol);
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            // Prerender estático: JS interop no disponible; el circuito interactivo lo reintentará.
        }
        catch { }
        return false;
    }

    private static bool Valida(Usuario? u, string? token) =>
        u is not null && u.Activo && !u.BloqueadoDefinitivamente
        && (u.BloqueadoHastaUtc is null || u.BloqueadoHastaUtc <= DateTime.UtcNow)
        && !string.IsNullOrEmpty(u.SesionToken) && !string.IsNullOrEmpty(token)
        && string.Equals(u.SesionToken, token, StringComparison.Ordinal);
}

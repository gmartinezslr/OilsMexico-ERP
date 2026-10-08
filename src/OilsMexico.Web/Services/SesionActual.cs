using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Web.Services;

/// <summary>Sesión persistente por navegador: sobrevive navegaciones y recargas del circuito.</summary>
public sealed class SesionActual(ProtectedSessionStorage storage, ISucursalContext ctx, ErpDbContext db) : ISesionActual
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

    public async Task CerrarAsync()
    {
        Sesion = null;
        await storage.DeleteAsync(Clave);
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
        catch { }
        return false;
    }

    private static bool Valida(Usuario? u, string? token) =>
        u is not null && u.Activo && !u.BloqueadoDefinitivamente
        && (u.BloqueadoHastaUtc is null || u.BloqueadoHastaUtc <= DateTime.UtcNow)
        && !string.IsNullOrEmpty(u.SesionToken) && !string.IsNullOrEmpty(token)
        && string.Equals(u.SesionToken, token, StringComparison.Ordinal);
}

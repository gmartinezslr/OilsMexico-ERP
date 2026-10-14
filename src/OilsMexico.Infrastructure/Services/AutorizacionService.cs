using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class AutorizacionService(ErpDbContext db, ISucursalContext ctx) : IAutorizacionService
{
    public async Task<bool> TienePermisoAsync(string permiso, CancellationToken ct = default)
    {
        if (!ctx.Establecida || string.IsNullOrWhiteSpace(permiso)) return false;

        return await db.RolPermisos.AsNoTracking()
            .AnyAsync(rp => rp.Rol.Nombre == ctx.Rol && rp.Permiso.Nombre == permiso, ct);
    }

    public async Task<IReadOnlySet<string>> ObtenerPermisosUsuarioAsync(CancellationToken ct = default)
    {
        if (!ctx.Establecida) return new HashSet<string>(StringComparer.Ordinal);

        return await db.RolPermisos.AsNoTracking()
            .Where(rp => rp.Rol.Nombre == ctx.Rol)
            .Select(rp => rp.Permiso.Nombre)
            .ToHashSetAsync(ct);
    }

    public async Task<IReadOnlyList<RolPermisoMatrizDto>> ObtenerMatrizAsync(CancellationToken ct = default)
    {
        ExigirAdmin();
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Nombre).ToListAsync(ct);
        var permisos = await db.Permisos.AsNoTracking().OrderBy(p => p.Descripcion).ToListAsync(ct);
        var asignados = await db.RolPermisos.AsNoTracking()
            .Select(rp => new { rp.RolId, rp.PermisoId })
            .ToListAsync(ct);
        var conjunto = asignados.Select(a => (a.RolId, a.PermisoId)).ToHashSet();

        return roles.SelectMany(rol => permisos.Select(permiso =>
        {
            var esAdmin = rol.Nombre == Roles.Admin;
            return new RolPermisoMatrizDto(
                rol.Id,
                rol.Nombre,
                permiso.Id,
                permiso.Nombre,
                permiso.Descripcion,
                conjunto.Contains((rol.Id, permiso.Id)),
                esAdmin);
        })).ToList();
    }

    public async Task GuardarMatrizAsync(
        IReadOnlyList<RolPermisoMatrizActualizacionDto> cambios,
        CancellationToken ct = default)
    {
        ExigirAdmin();
        var rolIds = cambios.Select(c => c.RolId).Distinct().ToList();
        if (rolIds.Count != cambios.Count)
            throw new ArgumentException("La matriz contiene roles duplicados.", nameof(cambios));

        var roles = await db.Roles.Where(r => rolIds.Contains(r.Id)).ToListAsync(ct);
        if (roles.Count != rolIds.Count)
            throw new ArgumentException("La matriz incluye un rol inexistente.", nameof(cambios));

        var permisosPorRol = cambios.ToDictionary(c => c.RolId, c => c.PermisoIds.Distinct().ToList());
        var idsSolicitados = permisosPorRol.Values.SelectMany(x => x).Distinct().ToList();
        var idsExistentes = await db.Permisos.Where(p => idsSolicitados.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (idsExistentes.Count != idsSolicitados.Count)
            throw new ArgumentException("La matriz incluye un permiso inexistente.", nameof(cambios));

        var roleAdmin = roles.SingleOrDefault(r => r.Nombre == Roles.Admin);
        if (roleAdmin is not null && permisosPorRol.TryGetValue(roleAdmin.Id, out var adminPermisos))
        {
            var todosLosPermisos = await db.Permisos.Select(p => p.Id).ToHashSetAsync(ct);
            if (!todosLosPermisos.SetEquals(adminPermisos))
                throw new InvalidOperationException("Los permisos del rol Admin son fijos y no se pueden reducir.");
        }

        foreach (var cambio in cambios.Where(c => roles.Single(r => r.Id == c.RolId).Nombre != Roles.Admin))
        {
            var existentes = await db.RolPermisos
                .Where(rp => rp.RolId == cambio.RolId)
                .ToListAsync(ct);
            db.RolPermisos.RemoveRange(existentes);
            db.RolPermisos.AddRange(permisosPorRol[cambio.RolId].Select(permisoId =>
                new RolPermiso { RolId = cambio.RolId, PermisoId = permisoId }));
        }

        await db.SaveChangesAsync(ct);
    }

    private void ExigirAdmin()
    {
        if (!ctx.EsAdmin)
            throw new UnauthorizedAccessException("La administración de permisos requiere una sesión Admin.");
    }
}

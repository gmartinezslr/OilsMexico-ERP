using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Persistence;

internal static class PermisosSeed
{
    private const string ClaveInicializada = "rbac_matriz_inicializada_v1";

    public static async Task AsegurarAsync(ErpDbContext db, CancellationToken ct = default)
    {
        if (await db.Configuracion.AnyAsync(c => c.Clave == ClaveInicializada, ct)) return;

        var roles = new List<Role>();
        foreach (var nombre in Roles.Todos)
        {
            var rol = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == nombre, ct);
            if (rol is null)
            {
                rol = new Role
                {
                    Nombre = nombre,
                    Descripcion = nombre switch
                    {
                        Roles.Admin => "Administración total del ERP.",
                        Roles.Vendedor => "Operación comercial y ventas.",
                        Roles.Almacen => "Operación de almacén y compras.",
                        Roles.Conta => "Contabilidad, cobranza y reportes financieros.",
                        _ => nombre
                    }
                };
                db.Roles.Add(rol);
                roles.Add(rol);
            }
            else
            {
                roles.Add(rol);
            }
        }

        var permisosActuales = await db.Permisos.ToListAsync(ct);
        var permisosPorNombre = permisosActuales.ToDictionary(p => p.Nombre, StringComparer.Ordinal);
        foreach (var definicion in PermisosPaginas.Todas)
        {
            var nombre = PermisosPaginas.Nombre(definicion.Ruta);
            if (!permisosPorNombre.ContainsKey(nombre))
            {
                var permiso = new Permiso
                {
                    Nombre = nombre,
                    Descripcion = $"Acceder a {definicion.Etiqueta}"
                };
                db.Permisos.Add(permiso);
                permisosPorNombre.Add(nombre, permiso);
            }
        }

        await db.SaveChangesAsync(ct);

        var asignacionesExistentes = await db.RolPermisos
            .Select(rp => new { rp.RolId, rp.PermisoId })
            .ToListAsync(ct);
        var conjuntoAsignaciones = asignacionesExistentes
            .Select(a => (a.RolId, a.PermisoId))
            .ToHashSet();
        foreach (var definicion in PermisosPaginas.Todas)
        {
            var permiso = permisosPorNombre[PermisosPaginas.Nombre(definicion.Ruta)];
            foreach (var nombreRol in definicion.RolesIniciales)
            {
                var rol = roles.Single(r => r.Nombre == nombreRol);
                if (conjuntoAsignaciones.Add((rol.Id, permiso.Id)))
                    db.RolPermisos.Add(new RolPermiso { RolId = rol.Id, PermisoId = permiso.Id });
            }
        }
        await db.SaveChangesAsync(ct);

        db.Configuracion.Add(new Configuracion { Clave = ClaveInicializada, Valor = "1" });
        await db.SaveChangesAsync(ct);
    }
}

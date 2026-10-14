using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Services;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using Xunit;

namespace OilsMexico.IntegrationTests;

public sealed class AutorizacionTests
{
    private static (ErpDbContext Db, SucursalContext Context) CrearDb(string nombre, string rol)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(nombre)
            .Options;
        var db = new ErpDbContext(options);
        var admin = new Role { Id = 1, Nombre = Roles.Admin };
        var vendedor = new Role { Id = 2, Nombre = Roles.Vendedor };
        var ventas = new Permiso { Id = 1, Nombre = PermisosPaginas.Nombre("ventas-pos") };
        var compras = new Permiso { Id = 2, Nombre = PermisosPaginas.Nombre("compras") };
        db.AddRange(admin, vendedor, ventas, compras);
        db.RolPermisos.AddRange(
            new RolPermiso { Rol = admin, Permiso = ventas },
            new RolPermiso { Rol = admin, Permiso = compras },
            new RolPermiso { Rol = vendedor, Permiso = ventas });
        db.SaveChanges();

        var context = new SucursalContext();
        context.Establecer(1, 7, rol);
        return (db, context);
    }

    [Fact]
    public async Task Permiso_es_fail_closed_sin_contexto_de_sesion()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(nameof(Permiso_es_fail_closed_sin_contexto_de_sesion))
            .Options;
        await using var db = new ErpDbContext(options);
        var servicio = new AutorizacionService(db, new SucursalContext());

        Assert.False(await servicio.TienePermisoAsync(PermisosPaginas.Nombre("ventas-pos")));
        Assert.Empty(await servicio.ObtenerPermisosUsuarioAsync());
    }

    [Fact]
    public async Task Consulta_resuelve_acceso_por_rol_y_permiso()
    {
        var (db, context) = CrearDb(nameof(Consulta_resuelve_acceso_por_rol_y_permiso), Roles.Vendedor);
        var servicio = new AutorizacionService(db, context);

        Assert.True(await servicio.TienePermisoAsync(PermisosPaginas.Nombre("ventas-pos")));
        Assert.False(await servicio.TienePermisoAsync(PermisosPaginas.Nombre("compras")));
    }

    [Fact]
    public async Task Guardar_matriz_no_permite_modificar_permisos_de_admin()
    {
        var (db, context) = CrearDb(nameof(Guardar_matriz_no_permite_modificar_permisos_de_admin), Roles.Admin);
        var servicio = new AutorizacionService(db, context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.GuardarMatrizAsync(
            [new RolPermisoMatrizActualizacionDto(1, [1])]));
        Assert.Equal(2, await db.RolPermisos.CountAsync(rp => rp.RolId == 1));
    }

    [Fact]
    public async Task Guardar_matriz_aplica_los_cambios_al_permiso_efectivo()
    {
        var (db, context) = CrearDb(nameof(Guardar_matriz_aplica_los_cambios_al_permiso_efectivo), Roles.Admin);
        var servicio = new AutorizacionService(db, context);

        await servicio.GuardarMatrizAsync(
        [
            new RolPermisoMatrizActualizacionDto(1, [1, 2]),
            new RolPermisoMatrizActualizacionDto(2, [2])
        ]);
        context.Establecer(1, 8, Roles.Vendedor);

        Assert.False(await servicio.TienePermisoAsync(PermisosPaginas.Nombre("ventas-pos")));
        Assert.True(await servicio.TienePermisoAsync(PermisosPaginas.Nombre("compras")));
        Assert.Equal(2, await db.RolPermisos.CountAsync(rp => rp.RolId == 1));
    }

    [Fact]
    public async Task Administrar_matriz_rechaza_sesion_no_admin()
    {
        var (db, context) = CrearDb(nameof(Administrar_matriz_rechaza_sesion_no_admin), Roles.Vendedor);
        var servicio = new AutorizacionService(db, context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ObtenerMatrizAsync());
    }
}

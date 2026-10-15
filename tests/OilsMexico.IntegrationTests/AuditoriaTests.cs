using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Application.Services;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using Xunit;

namespace OilsMexico.IntegrationTests;

public sealed class AuditoriaTests
{
    private static (ErpDbContext Db, SucursalContext Context) CrearDb(string nombre, string rol = Roles.Admin)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(nombre)
            .Options;
        var db = new ErpDbContext(options);
        db.Sucursales.Add(new Sucursal
        {
            Id = 1,
            Nombre = "Matriz",
            CodigoSucursal = "M01",
            RfcEmisor = "AAA010101AAA",
            Activa = true
        });
        db.Usuarios.Add(new Usuario
        {
            Id = 7,
            Nombre = "Operador",
            Rol = rol,
            SucursalId = 1,
            PinHash = "pin",
            PasswordHash = "password"
        });
        db.SaveChanges();

        var context = new SucursalContext();
        context.Establecer(1, 7, rol);
        return (db, context);
    }

    [Fact]
    public async Task Busqueda_filtra_por_fecha_usuario_y_accion()
    {
        var (db, context) = CrearDb(nameof(Busqueda_filtra_por_fecha_usuario_y_accion));
        var desde = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        db.AuditLogs.AddRange(
            new AuditLog
            {
                UsuarioId = 7,
                Tipo = "OVSuspendida",
                Detalle = "Orden OV-1",
                FechaUtc = desde.AddHours(4)
            },
            new AuditLog
            {
                UsuarioId = 7,
                Tipo = "OVConfirmada",
                Detalle = "Orden OV-2",
                FechaUtc = desde.AddHours(5)
            },
            new AuditLog
            {
                UsuarioId = 7,
                Tipo = "OVSuspendida",
                Detalle = "Fuera del rango",
                FechaUtc = desde.AddDays(1)
            });
        await db.SaveChangesAsync();

        var servicio = new AuditoriaService(db, context);
        var pagina = await servicio.BuscarAsync(
            desde, desde.AddDays(1), 7, "OVSuspendida", pagina: 1, porPagina: 25);

        var registro = Assert.Single(pagina.Registros);
        Assert.Equal("Operador", registro.Usuario);
        Assert.Equal("OVSuspendida", registro.Tipo);
        Assert.Equal("Orden OV-1", registro.Detalle);
        Assert.Equal(1, pagina.Total);
    }

    [Fact]
    public async Task Consulta_de_auditoria_rechaza_roles_no_admin()
    {
        var (db, context) = CrearDb(nameof(Consulta_de_auditoria_rechaza_roles_no_admin), Roles.Vendedor);
        var servicio = new AuditoriaService(db, context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.ListarTiposAsync());
    }

    [Fact]
    public async Task Transiciones_de_orden_registran_el_usuario_y_estado()
    {
        var (db, context) = CrearDb(nameof(Transiciones_de_orden_registran_el_usuario_y_estado));
        db.OrdenesVenta.Add(new OrdenVenta
        {
            Id = 11,
            SucursalId = 1,
            ClienteId = 1,
            UsuarioId = 7,
            FolioInterno = "OV-11",
            FechaEmision = DateTime.UtcNow,
            Estado = EstadoOrdenVenta.Borrador
        });
        await db.SaveChangesAsync();
        var servicio = new CotizacionesService(db, context, new VentasStub());

        await servicio.ConfirmarOrdenVentaAsync(11, new OrdenVentaConfirmarRequest(null, "Aprobada"));
        await servicio.SuspenderOrdenVentaAsync(11, "Validación");
        await servicio.ReanudarOrdenVentaAsync(11, "Validada");
        await servicio.CancelarOrdenVentaAsync(11, "Solicitud del cliente");

        var eventos = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(4, eventos.Count);
        Assert.Equal(
            new[] { "OVConfirmada", "OVSuspendida", "OVReanudada", "OVCancelada" },
            eventos.Select(e => e.Tipo));
        Assert.All(eventos, evento => Assert.Equal(7, evento.UsuarioId));
        Assert.Equal(nameof(EstadoOrdenVenta.Borrador), eventos[0].Anterior);
        Assert.Equal(nameof(EstadoOrdenVenta.Confirmada), eventos[0].Nuevo);
        Assert.Contains("Aprobada", eventos[0].Detalle);
    }

    private sealed class VentasStub : IVentasPosService, IOrdenVentaFacturacionService
    {
        public Task<List<ProductoDto>> BuscarProductosAsync(
            int sucursalId, string? filtro, CancellationToken ct = default) =>
            Task.FromResult(new List<ProductoDto>());

        public Task<VentaPosResult> RegistrarVentaAsync(
            VentaPosRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OrdenVentaFacturarResult> FacturarOrdenVentaAsync(
            int ordenId, OrdenVentaFacturarRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

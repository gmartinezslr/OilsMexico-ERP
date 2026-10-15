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

    [Fact]
    public async Task Operaciones_de_catalogo_compras_configuracion_y_caja_se_auditan()
    {
        var (db, context) = CrearDb(nameof(Operaciones_de_catalogo_compras_configuracion_y_caja_se_auditan));
        var catalogos = new CatalogosService(db, context);
        var producto = new ProductoCatalogoDto
        {
            Sku = "ACE-01", Nombre = "Aceite", Marca = "Marca", Viscosidad = "5W-30",
            PrecioVenta = 100m, PrecioUnitario = 100m, PrecioLista = 120m
        };
        var creado = await catalogos.GuardarProductoAsync(producto);
        Assert.True(creado.Ok);

        producto.Id = creado.Id;
        producto.PrecioVenta = 110m;
        producto.PrecioUnitario = 110m;
        var actualizado = await catalogos.GuardarProductoAsync(producto);
        Assert.True(actualizado.Ok);

        var proveedor = new Proveedor { Nombre = "Proveedor", CodigoPostal = "06600", Activo = true };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();
        var compras = new ComprasService(db, context, new CierreStub());
        await compras.CrearOrdenAsync(new CrearCompraRequest(
            1, proveedor.Id, null, null,
            [new CompraRenglonDto(creado.Id, null, "Litro", 1m, 2m, 50m)]));

        var configuracion = new ConfiguracionService(db, context);
        await configuracion.GuardarAsync("prueba_config", "nuevo");

        var caja = new CorteCajaService(db, context);
        var corte = await caja.AbrirAsync(1, 200m);
        await caja.CerrarAsync(new CerrarCorteRequest(corte.CorteId, 200m, "Cierre de prueba"));

        var eventos = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Contains(eventos, e => e.Tipo == "ProductoCreado" && e.UsuarioId == 7);
        Assert.Contains(eventos, e => e.Tipo == "ProductoActualizado"
            && e.Anterior!.Contains("PrecioVenta=100") && e.Nuevo!.Contains("PrecioVenta=110"));
        Assert.Contains(eventos, e => e.Tipo == "CompraCreada" && e.UsuarioId == 7);
        Assert.Contains(eventos, e => e.Tipo == "ConfiguracionActualizada"
            && e.Anterior is null && e.Nuevo == "nuevo" && e.UsuarioId == 7);
        Assert.Contains(eventos, e => e.Tipo == "CajaAbierta" && e.UsuarioId == 7);
        Assert.Contains(eventos, e => e.Tipo == "CajaCerrada" && e.UsuarioId == 7);
    }

    [Fact]
    public async Task Entrada_de_inventario_respeta_el_periodo_cerrado()
    {
        var (db, context) = CrearDb(nameof(Entrada_de_inventario_respeta_el_periodo_cerrado));
        var inventario = new InventarioService(db, context, new CierreStub(cerrado: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventario.EntradaCompraAsync(1, 12, null, 5m, 7));

        Assert.Empty(await db.Movimientos.ToListAsync());
        Assert.Empty(await db.InventarioSucursal.ToListAsync());
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Gasto_con_poliza_no_se_puede_editar_contablemente_ni_eliminar()
    {
        var (db, context) = CrearDb(nameof(Gasto_con_poliza_no_se_puede_editar_contablemente_ni_eliminar));
        var fecha = DateTime.UtcNow;
        var gasto = new Gasto
        {
            SucursalId = 1, UsuarioId = 7, Fecha = fecha, Categoria = "Servicios",
            Concepto = "Servicio", Importe = 100m, FormaPagoSat = "01"
        };
        db.Gastos.Add(gasto);
        await db.SaveChangesAsync();
        db.AsientosContables.Add(new AsientoContable
        {
            SucursalId = 1, UsuarioId = 7, Concepto = $"Gasto {gasto.Categoria} #{gasto.Id} {gasto.Concepto}"
        });
        await db.SaveChangesAsync();
        var servicio = new GastoService(
            db, context, new CuentasContablesService(db, context), new CierreStub());

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EditarAsync(gasto.Id,
            new CrearGastoRequest(1, fecha, "Servicios", "Servicio", 120m, "01", null, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EliminarAsync(gasto.Id));

        Assert.Single(await db.Gastos.ToListAsync());
        Assert.Single(await db.AsientosContables.ToListAsync());
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

    private sealed class CierreStub(bool cerrado = false) : ICierreContableService
    {
        public Task<List<PeriodoContableDto>> ListarAsync(int sucursalId, CancellationToken ct = default) =>
            Task.FromResult(new List<PeriodoContableDto>());

        public Task<bool> EstaCerradoAsync(int sucursalId, DateTime fecha, CancellationToken ct = default) =>
            Task.FromResult(cerrado);

        public Task<CierreContableResultDto> CerrarAsync(
            int sucursalId, int anio, int mes, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task ReabrirAsync(int sucursalId, int anio, int mes, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

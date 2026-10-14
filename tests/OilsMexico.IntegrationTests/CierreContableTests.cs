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

/// <summary>
/// Verificación del cierre de periodo contable (checklist §1.3): costo de ventas FIFO,
/// póliza de cierre (Costo de ventas vs. Almacén), bloqueo de escritura en periodos
/// cerrados, reapertura solo Admin y Estado de Resultados alimentado por la póliza.
/// EF InMemory (BD desechable por prueba); el periodo de prueba es el MES ANTERIOR al actual.
/// </summary>
public class CierreContableTests
{
    /// <summary>BD con sucursal/producto/cliente/proveedor + 2 compras (100 L a 10 y 100 L a 12)
    /// y una venta de 150 L en el mes anterior → costo FIFO esperado = 100×10 + 50×12 = 1600.</summary>
    private static (ErpDbContext db, ISucursalContext ctx, DateTime MesIni, int Anio, int Mes) Crear(string dbName)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(options);
        var mesIni = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);

        db.Sucursales.Add(new Sucursal { Id = 1, Nombre = "Matriz", CodigoSucursal = "M01", RfcEmisor = "AAA010101AAA", Activa = true });
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "Cliente", Rfc = "XAXX010101000", Activo = true });
        db.Proveedores.Add(new Proveedor { Id = 1, Nombre = "Proveedor", Rfc = "XAXX010101AAA", Activo = true });
        // PrecioCosto de catálogo 50: si el reporte lo usara, el costo daría 7500 (no 1600).
        db.Productos.Add(new Producto { Id = 1, Sku = "SKU1", Nombre = "Aceite 5W-30", Marca = "X", Viscosidad = "5W-30", PrecioCosto = 50m, PrecioVenta = 80m, Activo = true });

        db.Compras.AddRange(
            new Compra { Id = 1, SucursalId = 1, ProveedorId = 1, FolioInterno = "C-1", Estado = EstadoCompra.Recibida,
                Detalles = new List<CompraDetalle> { new() { Id = 1, CompraId = 1, ProductoId = 1, Cantidad = 100m, FactorConversion = 1m, CostoUnitario = 10m } } },
            new Compra { Id = 2, SucursalId = 1, ProveedorId = 1, FolioInterno = "C-2", Estado = EstadoCompra.Recibida,
                Detalles = new List<CompraDetalle> { new() { Id = 2, CompraId = 2, ProductoId = 1, Cantidad = 100m, FactorConversion = 1m, CostoUnitario = 12m } } });
        db.Movimientos.AddRange(
            new MovimientoInventario { Id = 1, SucursalId = 1, ProductoId = 1, CantidadLitros = 100m, Tipo = "COMPRA", ReferenciaId = 1, FechaUtc = mesIni.AddDays(-12) },
            new MovimientoInventario { Id = 2, SucursalId = 1, ProductoId = 1, CantidadLitros = 100m, Tipo = "COMPRA", ReferenciaId = 2, FechaUtc = mesIni.AddDays(-5) });
        db.Facturas.Add(new Factura
        {
            Id = 1, SucursalId = 1, ClienteId = 1, FolioInterno = "F-1",
            FechaEmision = mesIni.AddDays(5), Subtotal = 2000m, Iva = 320m, Total = 2320m,
            Estado = EstadoFactura.Timbrada,
            Detalles = new List<FacturaDetalle>
            {
                new() { Id = 1, ProductoId = 1, Cantidad = 150m, LitrosDescontados = 150m, PrecioUnitario = 40m, Importe = 6000m, UnidadNombre = "Litro" }
            }
        });
        db.Movimientos.Add(new MovimientoInventario
        {
            Id = 3, SucursalId = 1, ProductoId = 1, CantidadLitros = -150m,
            Tipo = "VENTA", ReferenciaId = 1, FechaUtc = mesIni.AddDays(5)
        });
        db.SaveChanges();

        var ctx = new SucursalContext();
        ctx.Establecer(1, 1, Roles.Admin);
        return (db, ctx, mesIni, mesIni.Year, mesIni.Month);
    }

    private static (CierreContableService cierre, ICuentasContablesService cuentas, IAsientoGeneradorService generador)
        Servicios(ErpDbContext db, ISucursalContext ctx)
    {
        var cuentas = new CuentasContablesService(db, ctx);
        var generador = new AsientoGeneradorService(db, ctx);
        return (new CierreContableService(db, ctx, generador, cuentas), cuentas, generador);
    }

    [Fact]
    public async Task FIFO_reconstruye_capas_por_costo_de_compra()
    {
        var (db, ctx, mesIni, anio, mes) = Crear(nameof(FIFO_reconstruye_capas_por_costo_de_compra));
        var (_, _, generador) = Servicios(db, ctx);

        var costo = await generador.CalcularCostoVentasFifoAsync(1, mesIni, mesIni.AddMonths(1).AddDays(-1));

        // 100 L de la capa 1 a 10 + 50 L de la capa 2 a 12 (NO 150 × PrecioCosto 50 = 7500).
        Assert.Equal(1600m, costo.Total);
        var renglon = Assert.Single(costo.Renglones);
        Assert.Equal(150m, renglon.Litros);
        Assert.Equal(1600m, renglon.Costo);
        Assert.Equal("SKU1", renglon.Sku);
    }

    [Fact]
    public async Task Asiento_costo_venta_usa_fallback_5000_vs_1200()
    {
        var (db, ctx, mesIni, _, _) = Crear(nameof(Asiento_costo_venta_usa_fallback_5000_vs_1200));
        var (_, _, generador) = Servicios(db, ctx);

        var lineas = await generador.GenerarAsientosDeCostoVentaAsync(1, mesIni, mesIni.AddMonths(1).AddDays(-1));

        Assert.Equal(2, lineas.Count);
        var debe = Assert.Single(lineas, l => l.Debe);
        var haber = Assert.Single(lineas, l => !l.Debe);
        Assert.Equal("5000", debe.CuentaCodigo);
        Assert.Equal("1200", haber.CuentaCodigo);
        Assert.Equal(1600m, debe.Importe);
        Assert.Equal(1600m, haber.Importe);
    }

    [Fact]
    public async Task Cierre_genera_poliza_marca_periodo_y_bloquea_escrituras()
    {
        var (db, ctx, mesIni, anio, mes) = Crear(nameof(Cierre_genera_poliza_marca_periodo_y_bloquea_escrituras));
        var (cierre, cuentas, _) = Servicios(db, ctx);

        var res = await cierre.CerrarAsync(1, anio, mes);

        Assert.True(res.Cerrado);
        Assert.Equal(1600m, res.CostoVentas);
        Assert.NotNull(res.AsientoId);

        // La póliza: débito 5000 (Costo de ventas) y crédito 1200 (Almacén), concepto marcador.
        var asiento = await db.AsientosContables.Include(a => a.Detalles)
            .SingleAsync(a => a.Id == res.AsientoId);
        Assert.Equal(CierresContables.TipoAsiento, asiento.Tipo);
        Assert.Equal(CierresContables.ConceptoCierre(anio, mes), asiento.Concepto);
        var detDebe = asiento.Detalles.Single(d => d.TipoMovimiento == MovimientoTipo.Debito);
        var detHaber = asiento.Detalles.Single(d => d.TipoMovimiento == MovimientoTipo.Credito);
        Assert.Equal("5000", (await db.CuentasContables.SingleAsync(c => c.Id == detDebe.CuentaId)).Codigo);
        Assert.Equal("1200", (await db.CuentasContables.SingleAsync(c => c.Id == detHaber.CuentaId)).Codigo);
        Assert.Equal(1600m, detDebe.Importe);
        Assert.Equal(1600m, detHaber.Importe);
        // Saldos reflejados (débito suma 5000, crédito resta 1200).
        Assert.Equal(1600m, (await db.CuentasContables.SingleAsync(c => c.Codigo == "5000")).SaldoActual);
        Assert.Equal(-1600m, (await db.CuentasContables.SingleAsync(c => c.Codigo == "1200")).SaldoActual);
        Assert.True(await cierre.EstaCerradoAsync(1, mesIni));

        // Bloqueo: un asiento fechado en el periodo cerrado se rechaza (incluso para Admin)...
        var id5000 = (await db.CuentasContables.SingleAsync(c => c.Codigo == "5000")).Id;
        var id1000 = (await db.CuentasContables.SingleAsync(c => c.Codigo == "1000")).Id;
        var bloqueado = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cuentas.RegistrarAsientoAsync(new RegistrarAsientoRequest(1, 1, "Operativo", "intento en mes cerrado",
            [
                new RegistrarDetalleAsientoRequest(id5000, MovimientoTipo.Debito, 10m, "x"),
                new RegistrarDetalleAsientoRequest(id1000, MovimientoTipo.Credito, 10m, "x")
            ], new DateTime(anio, mes, 15, 12, 0, 0, DateTimeKind.Utc))));
        Assert.Contains("cerrado", bloqueado.Message);

        // ...pero un asiento del mes abierto actual sí pasa.
        var abierto = await cuentas.RegistrarAsientoAsync(new RegistrarAsientoRequest(1, 1, "Operativo", "mes abierto",
        [
            new RegistrarDetalleAsientoRequest(id5000, MovimientoTipo.Debito, 10m, "x"),
            new RegistrarDetalleAsientoRequest(id1000, MovimientoTipo.Credito, 10m, "x")
        ]));
        Assert.True(abierto.Equilibrado);
    }

    [Fact]
    public async Task Reabrir_solo_Admin_y_revirtio_completo()
    {
        var (db, ctx, mesIni, anio, mes) = Crear(nameof(Reabrir_solo_Admin_y_revirtio_completo));
        var (cierre, cuentas, _) = Servicios(db, ctx);
        var res = await cierre.CerrarAsync(1, anio, mes);
        Assert.Equal(1600m, (await db.CuentasContables.SingleAsync(c => c.Codigo == "5000")).SaldoActual);

        // Conta cierra pero NO puede reabrir.
        var ctxConta = new SucursalContext();
        ctxConta.Establecer(1, 1, Roles.Conta);
        var cierreConta = new CierreContableService(db, ctxConta, new AsientoGeneradorService(db, ctxConta), cuentas);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => cierreConta.ReabrirAsync(1, anio, mes));

        // Admin reabre: desaparece la póliza, se revierten saldos y vuelve a aceptar asientos.
        await cierre.ReabrirAsync(1, anio, mes);
        Assert.False(await db.AsientosContables.AnyAsync(a => a.Id == res.AsientoId));
        Assert.False(await db.PeriodosContables.AnyAsync(p => p.SucursalId == 1 && p.Anio == anio && p.Mes == mes));
        Assert.Equal(0m, (await db.CuentasContables.SingleAsync(c => c.Codigo == "5000")).SaldoActual);
        Assert.Equal(0m, (await db.CuentasContables.SingleAsync(c => c.Codigo == "1200")).SaldoActual);

        var id5000 = (await db.CuentasContables.SingleAsync(c => c.Codigo == "5000")).Id;
        var id1000 = (await db.CuentasContables.SingleAsync(c => c.Codigo == "1000")).Id;
        var trasReabrir = await cuentas.RegistrarAsientoAsync(new RegistrarAsientoRequest(1, 1, "Operativo", "mes reabierto",
        [
            new RegistrarDetalleAsientoRequest(id5000, MovimientoTipo.Debito, 10m, "x"),
            new RegistrarDetalleAsientoRequest(id1000, MovimientoTipo.Credito, 10m, "x")
        ], new DateTime(anio, mes, 20, 12, 0, 0, DateTimeKind.Utc)));
        Assert.True(trasReabrir.Equilibrado);
    }

    [Fact]
    public async Task EstadoResultados_usa_FIFO_y_cuadra_con_poliza_de_cierre()
    {
        var (db, ctx, mesIni, anio, mes) = Crear(nameof(EstadoResultados_usa_FIFO_y_cuadra_con_poliza_de_cierre));
        var (cierre, _, generador) = Servicios(db, ctx);
        var reportes = new ReportesFinancierosService(db, ctx, generador);

        // Periodo abierto: FIFO en vivo (1600), no PrecioCosto×cantidad (7500).
        var abierto = await reportes.ResultadosAsync(1, mesIni, mesIni.AddMonths(1).AddDays(-1));
        Assert.Equal(1600m, abierto.CostoVentas);

        // Tras el cierre: el mismo número, ahora leído de la póliza de cierre (fuente contable).
        await cierre.CerrarAsync(1, anio, mes);
        var cerrado = await reportes.ResultadosAsync(1, mesIni, mesIni.AddMonths(1).AddDays(-1));
        Assert.Equal(1600m, cerrado.CostoVentas);
    }

    [Fact]
    public async Task REGLA1_vendedor_no_puede_cerrar()
    {
        var (db, _, _, anio, mes) = Crear(nameof(REGLA1_vendedor_no_puede_cerrar));
        var ctxVendedor = new SucursalContext();
        ctxVendedor.Establecer(1, 1, Roles.Vendedor);
        var cierre = new CierreContableService(db, ctxVendedor,
            new AsientoGeneradorService(db, ctxVendedor), new CuentasContablesService(db, ctxVendedor));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => cierre.CerrarAsync(1, anio, mes));
    }

    [Fact]
    public async Task Cierre_sin_ventas_cierra_sin_poliza()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(nameof(Cierre_sin_ventas_cierra_sin_poliza)).Options;
        var db = new ErpDbContext(options);
        db.Sucursales.Add(new Sucursal { Id = 1, Nombre = "M", CodigoSucursal = "M01", RfcEmisor = "AAA010101AAA", Activa = true });
        db.SaveChanges();
        var ctx = new SucursalContext();
        ctx.Establecer(1, 1, Roles.Admin);
        var (cierre, _, _) = Servicios(db, ctx);
        var mes = DateTime.Now.AddMonths(-1);

        var res = await cierre.CerrarAsync(1, mes.Year, mes.Month);

        Assert.True(res.Cerrado);
        Assert.Null(res.AsientoId);
        Assert.Equal(0m, res.CostoVentas);
        Assert.False(await db.AsientosContables.AnyAsync());
        Assert.True(await cierre.EstaCerradoAsync(1, mes));
    }

    [Fact]
    public async Task Periodo_futuro_no_se_puede_cerrar()
    {
        var (db, ctx, _, _, _) = Crear(nameof(Periodo_futuro_no_se_puede_cerrar));
        var (cierre, _, _) = Servicios(db, ctx);
        var futuro = DateTime.Now.AddMonths(1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cierre.CerrarAsync(1, futuro.Year, futuro.Month));
    }

    [Fact]
    public async Task Factura_cancelada_no_aporta_costo()
    {
        var (db, ctx, mesIni, _, _) = Crear(nameof(Factura_cancelada_no_aporta_costo));
        (await db.Facturas.SingleAsync(f => f.Id == 1)).Estado = EstadoFactura.Cancelada;
        await db.SaveChangesAsync();
        var (_, _, generador) = Servicios(db, ctx);

        var costo = await generador.CalcularCostoVentasFifoAsync(1, mesIni, mesIni.AddMonths(1).AddDays(-1));

        // Misma regla de ingreso que el Estado de Resultados: fuera canceladas y devoluciones.
        Assert.Equal(0m, costo.Total);
        Assert.Empty(costo.Renglones);
    }

    [Fact]
    public async Task Listar_refleja_cierres_con_costo()
    {
        var (db, ctx, _, anio, mes) = Crear(nameof(Listar_refleja_cierres_con_costo));
        var (cierre, _, _) = Servicios(db, ctx);
        await cierre.CerrarAsync(1, anio, mes);

        var lista = await cierre.ListarAsync(1);

        var periodo = Assert.Single(lista);
        Assert.Equal(anio, periodo.Anio);
        Assert.Equal(mes, periodo.Mes);
        Assert.Equal(1600m, periodo.CostoVentas);
        // Doble cierre: rechazado (bandera + marcador).
        await Assert.ThrowsAsync<InvalidOperationException>(() => cierre.CerrarAsync(1, anio, mes));
    }
}
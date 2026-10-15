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

[CollectionDefinition("PostgreSQL P1", DisableParallelization = true)]
public sealed class PostgresP1Collection : ICollectionFixture<PgFixture>
{
    public const string Name = "PostgreSQL P1";
}

[Collection(PostgresP1Collection.Name)]
public sealed class PostgresP1Tests(PgFixture fixture)
{
    [Fact]
    public async Task PostgreSQL_CxC_aplica_cobros_y_excluye_facturas_canceladas()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var sucursal = NuevaSucursal();
            var cliente = new Cliente { Nombre = "Cliente CxC", DiasCredito = 0 };
            db.Add(sucursal);
            await db.SaveChangesAsync();
            var vendedor = new Usuario { Nombre = "Vendedor CxC", SucursalId = sucursal.Id };
            db.AddRange(cliente, vendedor);
            await db.SaveChangesAsync();

            var vencida = NuevaFactura(sucursal.Id, cliente.Id, vendedor.Id, "PPD", 100m, DateTime.UtcNow.AddDays(-10));
            var cancelada = NuevaFactura(sucursal.Id, cliente.Id, vendedor.Id, "PPD", 80m, DateTime.UtcNow.AddDays(-10));
            cancelada.Estado = EstadoFactura.Cancelada;
            db.Facturas.AddRange(vencida, cancelada);
            await db.SaveChangesAsync();
            db.VentaCobros.Add(new VentaCobro
            {
                SucursalId = sucursal.Id,
                FacturaId = vencida.Id,
                ClienteId = cliente.Id,
                Monto = 30m,
                UsuarioId = 1
            });
            await db.SaveChangesAsync();

            var saldo = await new CobranzaService(db).SaldoVencidoPorClienteAsync(cliente.Id);

            Assert.Equal(70m, saldo);
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task PostgreSQL_CxP_lista_saldo_pendiente_y_los_parches_son_idempotentes()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await SchemaPatch.AplicarAsync(db);
            await PeriodoContablePatch.AplicarAsync(db);
            var sucursal = NuevaSucursal();
            var proveedor = new Proveedor { Nombre = "Prov", CodigoPostal = "06600" };
            db.AddRange(sucursal, proveedor);
            await db.SaveChangesAsync();
            db.Compras.Add(new Compra
            {
                SucursalId = sucursal.Id,
                ProveedorId = proveedor.Id,
                FolioInterno = "C-P1-01",
                Estado = EstadoCompra.Recibida,
                EstadoPago = EstadoPagoCompra.Parcial,
                FechaEmision = DateTime.UtcNow.AddDays(-5),
                Total = 120m,
                MontoPagado = 20m,
                UsuarioId = 1
            });
            await db.SaveChangesAsync();

            var ctx = new SucursalContext();
            ctx.Establecer(sucursal.Id, 1, Roles.Admin);
            var cuentas = await new ComprasService(db, ctx).CuentasPorPagarAsync(sucursal.Id, null);

            var saldo = Assert.Single(cuentas);
            Assert.Equal(100m, saldo.Saldo);
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task PostgreSQL_cierre_bloquea_asientos_en_el_periodo_cerrado()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        {
            var sucursal = NuevaSucursal();
            db.Sucursales.Add(sucursal);
            await db.SaveChangesAsync();
            await CuentasContablesService.AsegurarCatalogoAsync(db, sucursal.Id);
            var cuentas = await db.CuentasContables
                .Where(c => c.SucursalId == sucursal.Id)
                .ToDictionaryAsync(c => c.Codigo);
            var ctx = new SucursalContext();
            ctx.Establecer(sucursal.Id, 1, Roles.Admin);
            var servicioCuentas = new CuentasContablesService(db, ctx);
            var generador = new AsientoGeneradorService(db, ctx);
            var cierre = new CierreContableService(db, ctx, generador, servicioCuentas);
            var ahora = DateTime.Now;

            var resultado = await cierre.CerrarAsync(sucursal.Id, ahora.Year, ahora.Month);

            Assert.True(resultado.Cerrado);
            var fechaDelPeriodo = new DateTime(ahora.Year, ahora.Month, 15, 12, 0, 0, DateTimeKind.Local)
                .ToUniversalTime();
            await Assert.ThrowsAsync<InvalidOperationException>(() => servicioCuentas.RegistrarAsientoAsync(
                new RegistrarAsientoRequest(sucursal.Id, ctx.UsuarioId, "Operativo", "prueba PostgreSQL",
                [
                    new RegistrarDetalleAsientoRequest(cuentas["6000"].Id, MovimientoTipo.Debito, 10m, null),
                    new RegistrarDetalleAsientoRequest(cuentas["1000"].Id, MovimientoTipo.Credito, 10m, null)
                ], fechaDelPeriodo)));
            Assert.True(await cierre.EstaCerradoAsync(sucursal.Id, fechaDelPeriodo));

            await cierre.ReabrirAsync(sucursal.Id, ahora.Year, ahora.Month);
            Assert.False(await cierre.EstaCerradoAsync(sucursal.Id, fechaDelPeriodo));
            var reabierto = await servicioCuentas.RegistrarAsientoAsync(
                new RegistrarAsientoRequest(sucursal.Id, ctx.UsuarioId, "Operativo", "periodo reabierto",
                [
                    new RegistrarDetalleAsientoRequest(cuentas["6000"].Id, MovimientoTipo.Debito, 10m, null),
                    new RegistrarDetalleAsientoRequest(cuentas["1000"].Id, MovimientoTipo.Credito, 10m, null)
                ], fechaDelPeriodo));
            Assert.True(reabierto.Equilibrado);
        }
    }

    [Fact]
    public async Task PostgreSQL_estado_resultados_toma_ingresos_y_gastos_del_libro()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var sucursal = NuevaSucursal();
            db.Sucursales.Add(sucursal);
            await db.SaveChangesAsync();
            await CuentasContablesService.AsegurarCatalogoAsync(db, sucursal.Id);
            var cuentas = await db.CuentasContables
                .Where(c => c.SucursalId == sucursal.Id)
                .ToDictionaryAsync(c => c.Codigo);
            var ahora = DateTime.Now;
            var fechaContable = new DateTime(ahora.Year, ahora.Month, 15, 12, 0, 0, DateTimeKind.Local)
                .ToUniversalTime();
            var mes = new DateTime(ahora.Year, ahora.Month, 1);

            db.AsientosContables.AddRange(
                Asiento(sucursal.Id, "Venta V-P1", fechaContable,
                    (cuentas["1000"], MovimientoTipo.Debito, 116m),
                    (cuentas["3000"], MovimientoTipo.Credito, 100m),
                    (cuentas["4000"], MovimientoTipo.Credito, 16m)),
                Asiento(sucursal.Id, "Gasto P1", fechaContable,
                    (cuentas["6000"], MovimientoTipo.Debito, 25m),
                    (cuentas["1000"], MovimientoTipo.Credito, 25m)),
                new AsientoContable
                {
                    SucursalId = sucursal.Id,
                    UsuarioId = 1,
                    FechaUtc = fechaContable,
                    Tipo = CierresContables.TipoAsiento,
                    Concepto = CierresContables.ConceptoCierre(mes.Year, mes.Month),
                    Detalles =
                    [
                        Detalle(cuentas["5000"], MovimientoTipo.Debito, 10m),
                        Detalle(cuentas["1200"], MovimientoTipo.Credito, 10m)
                    ]
                });
            await db.SaveChangesAsync();

            var ctx = new SucursalContext();
            ctx.Establecer(sucursal.Id, 1, Roles.Admin);
            var generador = new AsientoGeneradorService(db, ctx);
            var reportes = new ReportesFinancierosService(db, ctx, generador);
            var reporte = await reportes.ResultadosAsync(
                sucursal.Id, mes, mes.AddMonths(1).AddDays(-1));
            var balanza = await reportes.BalanzaAsync(sucursal.Id, mes, mes.AddMonths(1).AddDays(-1));

            Assert.Equal(100m, reporte.VentasNetas);
            Assert.Equal(16m, reporte.IvaTrasladado);
            Assert.Equal(100m, reporte.Ventas);
            Assert.Equal(10m, reporte.CostoVentas);
            Assert.Equal(25m, reporte.GastosOperativos);
            Assert.Equal(balanza.TotalDebitos, balanza.TotalCreditos);
            await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task PostgreSQL_facturar_OV_registra_venta_libro_y_stock_en_una_transaccion()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        {
            var (sucursal, cliente, usuario, producto, unidad, orden) =
                await PrepararOrdenAsync(db, agregarReglaDesbalanceada: false);
            var ctx = new SucursalContext();
            ctx.Establecer(sucursal.Id, usuario.Id, Roles.Admin);
            var generador = new AsientoGeneradorService(db, ctx);
            var cuentas = new CuentasContablesService(db, ctx);
            var cierre = new CierreContableService(db, ctx, generador, cuentas);
            var ventas = new VentasService(
                db, ctx, new SelladoStub(), new PacStub(), cierre, new CobranzaService(db));

            var resultado = await ventas.FacturarOrdenVentaAsync(orden.Id,
                new OrdenVentaFacturarRequest("01", "PUE", "G03", RequiereFactura: false));

            Assert.Equal(EstadoOrdenVenta.Facturada, resultado.EstadoOrden);
            Assert.Equal(100m, resultado.Subtotal);
            Assert.Equal(16m, resultado.Iva);
            Assert.Equal(116m, resultado.Total);
            Assert.True(await db.AsientosContables.AnyAsync(a => a.Concepto == $"Venta {resultado.FolioFactura}"));
            Assert.Equal(9m, await db.InventarioSucursal
                .Where(i => i.SucursalId == sucursal.Id && i.ProductoId == producto.Id)
                .Select(i => i.StockActual).SingleAsync());
            Assert.Equal(resultado.FacturaId, await db.OrdenesVenta
                .Where(o => o.Id == orden.Id).Select(o => o.FacturaId).SingleAsync());
            Assert.Equal(unidad.Id, await db.Facturas.Where(f => f.Id == resultado.FacturaId)
                .SelectMany(f => f.Detalles).Select(d => d.UnidadMedidaId).SingleAsync());
            var desde = DateTime.UtcNow.Date.AddDays(-1);
            var hasta = DateTime.UtcNow.Date.AddDays(1);
            var reportes = new ReportesFinancierosService(db, ctx, new AsientoGeneradorService(db, ctx));
            var existencias = await reportes.ExistenciasAsync(sucursal.Id, desde, hasta);
            var existencia = Assert.Single(existencias.Renglones);
            Assert.Equal(10m, existencia.Inicial);
            Assert.Equal(1m, existencia.Ventas);
            Assert.Equal(9m, existencia.Final);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ventas.FacturarOrdenVentaAsync(
                orden.Id, new OrdenVentaFacturarRequest("01", "PUE", "G03", RequiereFactura: false)));
        }
    }

    [Fact]
    public async Task PostgreSQL_credito_bloquea_PPD_sin_limite_y_saldo_vencido_sobre_limite()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        {
            var (sucursal, cliente, usuario, _, _, orden) =
                await PrepararOrdenAsync(db, agregarReglaDesbalanceada: false);
            var ctx = new SucursalContext();
            ctx.Establecer(sucursal.Id, usuario.Id, Roles.Admin);
            var generador = new AsientoGeneradorService(db, ctx);
            var cuentas = new CuentasContablesService(db, ctx);
            var cierre = new CierreContableService(db, ctx, generador, cuentas);
            var ventas = new VentasService(
                db, ctx, new SelladoStub(), new PacStub(), cierre, new CobranzaService(db));

            await Assert.ThrowsAsync<InvalidOperationException>(() => ventas.FacturarOrdenVentaAsync(
                orden.Id, new OrdenVentaFacturarRequest("99", "PPD", "G03", RequiereFactura: false)));
            Assert.False(await db.Facturas.AnyAsync(f => f.SucursalId == sucursal.Id));

            cliente.LimiteCredito = 50m;
            db.Facturas.Add(new Factura
            {
                SucursalId = sucursal.Id,
                ClienteId = cliente.Id,
                VendedorId = usuario.Id,
                FolioInterno = $"F-{Guid.NewGuid():N}"[..18],
                FechaEmision = DateTime.UtcNow.AddDays(-5),
                Subtotal = 100m,
                Total = 100m,
                MetodoPagoSat = "PPD",
                Estado = EstadoFactura.Pendiente
            });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => ventas.FacturarOrdenVentaAsync(
                orden.Id, new OrdenVentaFacturarRequest("99", "PPD", "G03", RequiereFactura: false)));
        }
    }

    [Fact]
    public async Task PostgreSQL_facturar_OV_revierte_venta_y_stock_si_falla_la_poliza()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        {
            var (sucursal, _, usuario, producto, _, orden) =
                await PrepararOrdenAsync(db, agregarReglaDesbalanceada: true);
            var ctx = new SucursalContext();
            ctx.Establecer(sucursal.Id, usuario.Id, Roles.Admin);
            var generador = new AsientoGeneradorService(db, ctx);
            var cuentas = new CuentasContablesService(db, ctx);
            var cierre = new CierreContableService(db, ctx, generador, cuentas);
            var ventas = new VentasService(
                db, ctx, new SelladoStub(), new PacStub(), cierre, new CobranzaService(db));

            await Assert.ThrowsAsync<InvalidOperationException>(() => ventas.FacturarOrdenVentaAsync(
                orden.Id, new OrdenVentaFacturarRequest("01", "PUE", "G03", RequiereFactura: false)));

            var persistedOrder = await db.OrdenesVenta.AsNoTracking().SingleAsync(o => o.Id == orden.Id);
            Assert.Equal(EstadoOrdenVenta.Confirmada, persistedOrder.Estado);
            Assert.Null(persistedOrder.FacturaId);
            Assert.False(await db.Facturas.AnyAsync(f => f.SucursalId == sucursal.Id));
            Assert.Equal(10m, await db.InventarioSucursal
                .Where(i => i.SucursalId == sucursal.Id && i.ProductoId == producto.Id)
                .Select(i => i.StockActual).SingleAsync());
            Assert.False(await db.Movimientos.AnyAsync(m => m.SucursalId == sucursal.Id && m.Tipo == "VENTA"));
        }
    }

    private static async Task<(Sucursal Sucursal, Cliente Cliente, Usuario Usuario, Producto Producto,
        UnidadMedida Unidad, OrdenVenta Orden)> PrepararOrdenAsync(
            ErpDbContext db, bool agregarReglaDesbalanceada)
    {
        var sucursal = NuevaSucursal();
        db.Sucursales.Add(sucursal);
        await db.SaveChangesAsync();
        var cliente = new Cliente { Nombre = "Cliente OV", DiasCredito = 0 };
        var usuario = new Usuario { Nombre = "Vendedor OV", SucursalId = sucursal.Id };
        var producto = new Producto
        {
            Sku = $"P1-{Guid.NewGuid():N}"[..18],
            Nombre = "Aceite de prueba",
            Marca = "P1",
            Viscosidad = "15W-40",
            TipoBase = "Mineral",
            Activo = true
        };
        var unidad = new UnidadMedida
        {
            Producto = producto,
            UnidadNombre = "Litro",
            FactorConversion = 1m
        };
        db.AddRange(cliente, usuario, producto, unidad);
        await db.SaveChangesAsync();
        db.InventarioSucursal.Add(new InventarioSucursal
        {
            SucursalId = sucursal.Id,
            ProductoId = producto.Id,
            StockActual = 10m
        });
        var orden = new OrdenVenta
        {
            SucursalId = sucursal.Id,
            ClienteId = cliente.Id,
            VendedorId = usuario.Id,
            UsuarioId = usuario.Id,
            FolioInterno = $"OV-{Guid.NewGuid():N}"[..18],
            Estado = EstadoOrdenVenta.Confirmada,
            Subtotal = 100m,
            Iva = 16m,
            Total = 116m,
            Detalles =
            [
                new OrdenVentaDetalle
                {
                    ProductoId = producto.Id,
                    UnidadMedidaId = unidad.Id,
                    UnidadNombre = unidad.UnidadNombre,
                    Cantidad = 1m,
                    PrecioUnitario = 100m,
                    Importe = 100m
                }
            ]
        };
        db.OrdenesVenta.Add(orden);
        await CuentasContablesService.AsegurarCatalogoAsync(db, sucursal.Id);
        if (agregarReglaDesbalanceada)
        {
            var cuenta = await db.CuentasContables.SingleAsync(c =>
                c.SucursalId == sucursal.Id && c.Codigo == "3000");
            db.ReglasAsiento.Add(new ReglaAsiento
            {
                Evento = "Venta",
                CuentaId = cuenta.Id,
                Debe = false,
                MontoOrigen = "total",
                Activo = true
            });
        }
        await db.SaveChangesAsync();
        return (sucursal, cliente, usuario, producto, unidad, orden);
    }

    private static Sucursal NuevaSucursal() => new()
    {
        Nombre = "Sucursal pruebas P1",
        CodigoSucursal = $"P1-{Guid.NewGuid():N}"[..10],
        RfcEmisor = "AAA010101AAA"
    };

    private static Factura NuevaFactura(
        int sucursalId, int clienteId, int vendedorId, string metodoPago, decimal total, DateTime fecha) => new()
    {
        SucursalId = sucursalId,
        ClienteId = clienteId,
        FolioInterno = $"F-{Guid.NewGuid():N}"[..18],
        VendedorId = vendedorId,
        MetodoPagoSat = metodoPago,
        FechaEmision = fecha,
        Total = total,
        Subtotal = total,
        Estado = EstadoFactura.Pendiente
    };

    private static AsientoContable Asiento(
        int sucursalId, string concepto, DateTime fecha, params (CuentaContable Cuenta, int Movimiento, decimal Importe)[] lineas) =>
        new()
        {
            SucursalId = sucursalId,
            UsuarioId = 1,
            FechaUtc = fecha,
            Tipo = "Operativo",
            Concepto = concepto,
            Detalles = lineas.Select(l => Detalle(l.Cuenta, l.Movimiento, l.Importe)).ToList()
        };

    private static DetalleAsiento Detalle(CuentaContable cuenta, int movimiento, decimal importe) => new()
    {
        Cuenta = cuenta,
        TipoMovimiento = movimiento,
        Importe = importe
    };

    private sealed class SelladoStub : ICfdiSelladoService
    {
        public Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarAsync(
            int facturaId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarNotaCreditoAsync(
            int notaId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarRepAsync(
            int repId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class PacStub : IPacTimbradoService
    {
        public Task<(Guid Uuid, string XmlTimbrado)> TimbrarAsync(
            string xmlSellado, CancellationToken ct = default) => throw new NotSupportedException();

        public Task CancelarAsync(
            Guid uuid, string motivoSat, string? folioSustitucion = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<string> EstadoSatAsync(Guid uuid, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

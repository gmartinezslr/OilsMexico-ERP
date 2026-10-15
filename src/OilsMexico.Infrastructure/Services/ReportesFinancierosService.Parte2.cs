using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Reportes financieros §1.4 (parte 2: estado de resultados, cambio de existencias,
/// indicadores). El helper <see cref="Rango"/> y el primary constructor viven en la
/// parte 1; aquí NO se redefinen (una segunda definición daría error de compilación).
/// </summary>
public sealed partial class ReportesFinancierosService
{
    /// <summary>Estado de Resultados por sucursal y periodo.</summary>
    /// <remarks>
    /// §1.4b (unificación con el libro contable). Ingresos, IVA y gastos se derivan de las
    /// pólizas; así el estado de resultados concilia con la balanza. Las pólizas de venta se
    /// generan dentro de la misma transacción que la venta.
    /// <list type="bullet">
    /// <item><b>Ventas / IVA</b>: movimiento neto de las cuentas contables de ingreso y de IVA.</item>
    /// <item><b>Costo de ventas</b>: ya unificado con el libro — meses cerrados leen su póliza de
    /// cierre; meses abiertos usan el mismo motor FIFO que la póliza (ver <c>CostoVentasDelRangoAsync</c>).</item>
    /// <item><b>Gastos operativos</b>: derivados de asientos contables (cuentas Tipo = 5), sin
    /// incluir la cuenta 5000 de costo de ventas, que se informa por separado.</item>
    /// </list>
    /// El contrato del DTO (<see cref="EstadoResultadosDto"/>) no cambia.
    /// </remarks>
    public async Task<EstadoResultadosDto> ResultadosAsync(
        int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var (ini, fin) = Rango(desde, hasta);

        var ingresos = await IngresosContablesDelRangoAsync(suc, ini, fin, ct);
        var ventasNetas = Math.Round(ingresos.VentasNetas, 2);
        var ivaTrasladado = Math.Round(ingresos.IvaTrasladado, 2);
        // §1.4b: gastos operativos desde asientos contables (cuentas Tipo=5 Gasto), fuente de verdad.
        var gastos = Math.Round(await GastosOperativosDelRangoAsync(suc, ini, fin, ct), 2);

        // Costo de ventas ya unificado con el libro (cierre FIFO cerrado o motor FIFO en vivo).
        var costoVentas = Math.Round(await CostoVentasDelRangoAsync(suc, desde, hasta, ct), 2);
        var utilidadBruta = Math.Round(ventasNetas - costoVentas, 2);
        var utilidadOperacion = Math.Round(utilidadBruta - gastos, 2);
        var utilidadNeta = utilidadOperacion; // sin ISR/IVA retenido modelados en el ERP.
        var margenBruto = ventasNetas > 0 ? Math.Round(utilidadBruta / ventasNetas * 100m, 2) : 0m;
        var margenNeto = ventasNetas > 0 ? Math.Round(utilidadNeta / ventasNetas * 100m, 2) : 0m;

        return new EstadoResultadosDto(
            suc, desde.Date, hasta.Date,
            ventasNetas, 0m, ventasNetas,
            costoVentas, utilidadBruta, gastos,
            utilidadOperacion, ivaTrasladado, utilidadNeta,
            margenBruto, margenNeto);
    }

    private async Task<(decimal VentasNetas, decimal IvaTrasladado)> IngresosContablesDelRangoAsync(
        int suc, DateTime ini, DateTime fin, CancellationToken ct)
    {
        var movs = await db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable!.SucursalId == suc
                && d.AsientoContable.FechaUtc >= ini && d.AsientoContable.FechaUtc < fin
                && (d.Cuenta.Tipo == 4 || (d.Cuenta.Tipo == 2 && d.Cuenta.Codigo == "4000")))
            .GroupBy(d => new { d.Cuenta.Tipo, d.Cuenta.Codigo })
            .Select(g => new
            {
                g.Key.Tipo,
                g.Key.Codigo,
                Debitos = g.Where(d => d.TipoMovimiento == MovimientoTipo.Debito).Sum(d => d.Importe),
                Creditos = g.Where(d => d.TipoMovimiento == MovimientoTipo.Credito).Sum(d => d.Importe)
            })
            .ToListAsync(ct);

        return (
            movs.Where(m => m.Tipo == 4).Sum(m => m.Creditos - m.Debitos),
            movs.Where(m => m.Tipo == 2 && m.Codigo == "4000").Sum(m => m.Creditos - m.Debitos));
    }

    /// <summary>
    /// Gastos operativos del rango sumados desde los asientos contables: movimiento neto de las
    /// cuentas de resultado de Gasto (Tipo = 5), es decir débitos − créditos (una reversión de un
    /// gasto resta). Es la MISMA fuente que usa el Balance/Balanza, por lo que ambos cuadran.
    /// </summary>
    private async Task<decimal> GastosOperativosDelRangoAsync(int suc, DateTime ini, DateTime fin, CancellationToken ct)
    {
        var movs = await db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable!.SucursalId == suc
                && d.AsientoContable.FechaUtc >= ini && d.AsientoContable.FechaUtc < fin
                && d.Cuenta.Tipo == 5 && d.Cuenta.Codigo != "5000") // Costos se muestran por separado.
            .GroupBy(d => d.CuentaId)
            .Select(g => new
            {
                Debitos = g.Where(d => d.TipoMovimiento == MovimientoTipo.Debito).Sum(d => d.Importe),
                Creditos = g.Where(d => d.TipoMovimiento == MovimientoTipo.Credito).Sum(d => d.Importe)
            })
            .ToListAsync(ct);

        return movs.Sum(m => m.Debitos - m.Creditos);
    }

    /// <summary>Cambio de existencias: inicial + compras − ventas ± ajustes ± traspasos.</summary>
    public async Task<CambioExistenciasDto> ExistenciasAsync(
        int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var (ini, fin) = Rango(desde, hasta);

        // Movimientos del periodo por producto (netos del kardex). El Tipo es texto:
        // VENTA (negativa) | COMPRA (positiva) | AJUSTE | TRASPASO_IN | TRASPASO_OUT | DEVOLUCION.
        var movs = await db.Movimientos.AsNoTracking()
            .Where(m => m.SucursalId == suc && m.FechaUtc >= ini && m.FechaUtc < fin)
            .GroupBy(m => m.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Compras = g.Where(m => m.Tipo == "COMPRA").Sum(m => m.CantidadLitros),
                Ventas = g.Where(m => m.Tipo == "VENTA").Sum(m => m.CantidadLitros),
                Ajustes = g.Where(m => m.Tipo == "AJUSTE").Sum(m => m.CantidadLitros),
                Traspasos = g.Where(m => m.Tipo.StartsWith("TRASPASO")).Sum(m => m.CantidadLitros),
                Devoluciones = g.Where(m => m.Tipo == "DEVOLUCION").Sum(m => m.CantidadLitros),
                NetoPeriodo = g.Sum(m => m.CantidadLitros)
            })
            .ToDictionaryAsync(x => x.ProductoId, ct);

        // Stock actual (tiempo real) por producto. No hay histórico de inventario,
        // así que el inicial se deriva restando el neto del periodo al stock actual.
        var stocks = await db.InventarioSucursal.AsNoTracking()
            .Where(i => i.SucursalId == suc)
            .GroupBy(i => i.ProductoId)
            .Select(g => new { ProductoId = g.Key, Stock = g.Sum(i => i.StockActual) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Stock, ct);

        var todosIds = new HashSet<int>(stocks.Keys);
        todosIds.UnionWith(movs.Keys);

        var nombres = await db.Productos.AsNoTracking()
            .Where(p => todosIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Sku, p.Nombre })
            .ToDictionaryAsync(x => x.Id, ct);

        var ren = new List<ExistenciaRenglonDto>();
        foreach (var id in todosIds.OrderBy(id => id))
        {
            var m = movs.TryGetValue(id, out var mv) ? mv : null;
            var compras = m?.Compras ?? 0m;
            var ventas = -(m?.Ventas ?? 0m);            // kardex VENTA es negativa
            var devol = m?.Devoluciones ?? 0m;          // kardex DEVOLUCION es positiva
            var ajustes = m?.Ajustes ?? 0m;
            var traspasos = m?.Traspasos ?? 0m;
            var netoPeriodo = m?.NetoPeriodo ?? 0m;

            var stockActual = stocks.TryGetValue(id, out var s) ? s : 0m;
            var inicial = stockActual - netoPeriodo;
            var p = nombres.TryGetValue(id, out var n) ? n : null;
            ren.Add(new ExistenciaRenglonDto(
                id, p?.Sku ?? "", p?.Nombre ?? "",
                Math.Round(inicial, 2), Math.Round(compras, 2), Math.Round(ventas, 2),
                Math.Round(ajustes, 2), Math.Round(traspasos + devol, 2),
                Math.Round(inicial + compras - ventas + ajustes + traspasos + devol, 2)));
        }

        return new CambioExistenciasDto(
            suc, desde.Date, hasta.Date, ren,
            Math.Round(ren.Sum(r => r.Inicial), 2), Math.Round(ren.Sum(r => r.Compras), 2),
            Math.Round(ren.Sum(r => r.Ventas), 2), Math.Round(ren.Sum(r => r.Ajustes), 2),
            Math.Round(ren.Sum(r => r.Traspasos), 2), Math.Round(ren.Sum(r => r.Final), 2));
    }

    /// <summary>
    /// Indicadores por sucursal y periodo (margen, rotación, días de inventario).
    /// Con <paramref name="sucursalId"/> null: Admin ve todas las sucursales activas;
    /// el resto solo la suya (REGLA #1).
    /// </summary>
    public async Task<List<IndicadoresSucursalDto>> IndicadoresAsync(
        DateTime desde, DateTime hasta, int? sucursalId = null, CancellationToken ct = default)
    {
        List<int> sucursales;
        if (sucursalId.HasValue)
            sucursales = [ctx.ResolverSucursalAutorizada(sucursalId.Value)];
        else if (ctx.EsAdmin)
            sucursales = await db.Sucursales.AsNoTracking()
                .Where(s => s.Activa).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);
        else
            sucursales = [ctx.SucursalId];

        var lista = new List<IndicadoresSucursalDto>(sucursales.Count);
        foreach (var suc in sucursales)
            lista.Add(await IndicadoresDeSucursalAsync(suc, desde, hasta, ct));
        return lista;
    }

    private async Task<IndicadoresSucursalDto> IndicadoresDeSucursalAsync(
        int suc, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var (ini, fin) = Rango(desde, hasta);
        var sucursalNombre = await db.Sucursales.AsNoTracking()
            .Where(s => s.Id == suc).Select(s => s.Nombre).FirstOrDefaultAsync(ct) ?? "";

        var res = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == suc
                && f.FechaEmision >= ini && f.FechaEmision < fin
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion)
            .Select(f => new { f.Total, f.Detalles.Count })
            .ToListAsync(ct);

        var ventasNetas = res.Sum(r => r.Total);
        var numVentas = res.Count;

        var costoVentas = Math.Round(await CostoVentasDelRangoAsync(suc, desde, hasta, ct), 2);

        var utilidadBruta = Math.Round(ventasNetas - costoVentas, 2);
        var margen = ventasNetas > 0 ? Math.Round(utilidadBruta / ventasNetas * 100m, 2) : 0m;

        var unidadesVendidas = await db.FacturaDetalles.AsNoTracking()
            .Where(d => d.Factura!.SucursalId == suc
                && d.Factura.FechaEmision >= ini && d.Factura.FechaEmision < fin
                && d.Factura.Estado != EstadoFactura.Cancelada
                && d.Factura.Estado != EstadoFactura.Devolucion)
            .SumAsync(d => d.Cantidad, ct);

        // Inventario promedio: (stock inicial del periodo + stock actual) / 2,
        // donde inicial = actual − neto de movimientos del periodo.
        var stockActualTotal = await db.InventarioSucursal.AsNoTracking()
            .Where(i => i.SucursalId == suc).SumAsync(i => i.StockActual, ct);
        var netoPeriodoTotal = await db.Movimientos.AsNoTracking()
            .Where(m => m.SucursalId == suc && m.FechaUtc >= ini && m.FechaUtc < fin)
            .SumAsync(m => m.CantidadLitros, ct);
        var invProm = (stockActualTotal - netoPeriodoTotal + stockActualTotal) / 2m;

        var rotacion = invProm > 0 ? Math.Round(ventasNetas / invProm, 2) : 0m;
        var diasInv = invProm > 0 && ventasNetas > 0 ? Math.Round(invProm / (ventasNetas / 30m), 2) : 0m;
        var ticket = unidadesVendidas > 0 ? Math.Round(ventasNetas / unidadesVendidas, 2) : 0m;

        return new IndicadoresSucursalDto(
            suc, sucursalNombre, desde.Date, hasta.Date,
            ventasNetas, utilidadBruta, margen,
            rotacion, diasInv, ticket, numVentas, Math.Round(invProm, 2));
    }

    /// <summary>
    /// Costo de ventas del rango (§1.3): los meses COMPLETOS cerrados leen el importe de su póliza
    /// de cierre (fuente contable); el resto del rango se calcula en vivo con el motor FIFO del
    /// IAsientoGeneradorService (mismo algoritmo que usa el cierre, por lo que ambos cuadran).
    /// </summary>
    private async Task<decimal> CostoVentasDelRangoAsync(int suc, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var total = 0m;
        var finExclusivo = hasta.Date.AddDays(1);
        for (var mes = new DateTime(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
        {
            var mesFin = mes.AddMonths(1); // exclusivo
            var pedIni = desde > mes ? desde : mes;
            var pedFin = finExclusivo < mesFin ? finExclusivo : mesFin;
            var mesCompleto = pedIni == mes && pedFin == mesFin;

            decimal? costoCerrado = mesCompleto
                ? await CostoDelAsientoCierreAsync(suc, mes.Year, mes.Month, ct)
                : null;
            total += costoCerrado
                ?? (await generador.CalcularCostoVentasFifoAsync(suc, pedIni, pedFin.AddDays(-1), ct)).Total;
        }
        return total;
    }

    /// <summary>Importe del asiento de cierre FIFO del periodo; null si el periodo no tiene póliza.</summary>
    private async Task<decimal?> CostoDelAsientoCierreAsync(int suc, int anio, int mes, CancellationToken ct)
    {
        var concepto = CierresContables.ConceptoCierre(anio, mes);
        var q = db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable!.SucursalId == suc
                && d.AsientoContable.Tipo == CierresContables.TipoAsiento
                && d.AsientoContable.Concepto == concepto
                && d.TipoMovimiento == MovimientoTipo.Debito);
        return await q.AnyAsync(ct) ? Math.Round(await q.SumAsync(d => d.Importe, ct), 2) : null;
    }
}

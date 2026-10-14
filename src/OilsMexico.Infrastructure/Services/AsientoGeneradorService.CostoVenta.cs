using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Costo de ventas FIFO + asiento "Costo de Ventas vs. Almacén" (checklist §1.3).
///
/// El costo por lote NO está almacenado (InventarioLote no guarda costo), así que las capas
/// se reconstruyen replantando el kardex de la sucursal en orden cronológico:
///  - entradas COMPRA → capa con costo por litro del renglón de compra (CostoUnitario / FactorConversion);
///    respaldos: costo de la capa vigente y, sin historia, Producto.PrecioCosto del catálogo;
///  - salidas (VENTA, TRASPASO_OUT, AJUSTE negativo) → consumen las capas por orden (FIFO);
///  - DEVOLUCION/TRASPASO_IN/AJUSTE positivo → reponen capa al costo vigente.
/// El costo de las salidas VENTA dentro del rango se atribuye a la factura que las originó y
/// solo si la factura sigue activa (misma regla de ingreso que el Estado de Resultados:
/// no cancelada ni devuelta), para que ingresos y costos cuadren entre sí.
/// </summary>
public sealed partial class AsientoGeneradorService
{
    private const decimal Eps = 0.0000001m;

    /// <summary>Capa FIFO en memoria (cantidad en litros + costo por litro).</summary>
    private sealed class Capa
    {
        public decimal Cantidad;
        public decimal CostoLitro;
    }

    public async Task<CostoVentasFifoDto> CalcularCostoVentasFifoAsync(
        int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var (ini, fin) = Rango(desde, hasta);

        // Facturas del periodo cuyo costo cuenta (misma regla de ingreso que ResultadosAsync).
        var facturasActivas = (await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == suc
                && f.FechaEmision >= ini && f.FechaEmision < fin
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion)
            .Select(f => f.Id).ToListAsync(ct)).ToHashSet();

        // Productos con salida VENTA en el periodo: los únicos que pueden aportar costo.
        var productos = await db.Movimientos.AsNoTracking()
            .Where(m => m.SucursalId == suc && m.FechaUtc >= ini && m.FechaUtc < fin && m.Tipo == "VENTA")
            .Select(m => m.ProductoId).Distinct().ToListAsync(ct);
        if (productos.Count == 0)
            return new CostoVentasFifoDto(suc, desde.Date, hasta.Date, 0m, []);

        // Historial completo hasta el fin del periodo (el orden FEFO del POS no importa aquí:
        // el costo se asigna por orden cronológico de entrada/salida del kardex).
        var historial = await db.Movimientos.AsNoTracking()
            .Where(m => m.SucursalId == suc && productos.Contains(m.ProductoId) && m.FechaUtc < fin)
            .OrderBy(m => m.ProductoId).ThenBy(m => m.FechaUtc).ThenBy(m => m.Id)
            .Select(m => new { m.ProductoId, m.Tipo, m.CantidadLitros, m.ReferenciaId, m.FechaUtc })
            .ToListAsync(ct);
        // Costo por litro de las compras referenciadas por el kardex (ReferenciaId → compra).
        var referencias = historial
            .Where(m => m.Tipo == "COMPRA" && m.ReferenciaId.HasValue)
            .Select(m => m.ReferenciaId!.Value).Distinct().ToList();
        var costoPorCompra = (await db.CompraDetalles.AsNoTracking()
                .Where(d => referencias.Contains(d.CompraId) && productos.Contains(d.ProductoId))
                .ToListAsync(ct))
            .GroupBy(d => (d.CompraId, d.ProductoId))
            .ToDictionary(g => g.Key, g =>
            {
                var litros = g.Sum(d => d.Cantidad * d.FactorConversion);
                return litros > 0 ? g.Sum(d => d.Cantidad * d.CostoUnitario) / litros : 0m;
            });

        var catalogo = await db.Productos.AsNoTracking()
            .Where(p => productos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.Sku, p.Nombre, p.PrecioCosto), ct);

        var renglones = new List<CostoVentasRenglonDto>();
        decimal total = 0m;
        foreach (var prodId in productos)
        {
            catalogo.TryGetValue(prodId, out var prod);
            var respaldo = prod.PrecioCosto; // sin compra que sustente el costo: catálogo
            var capas = new List<Capa>();
            var puntero = 0;                 // capa vigente para consumir
            decimal costoProducto = 0m;
            decimal litrosVenta = 0m;

            foreach (var mov in historial.Where(h => h.ProductoId == prodId))
            {
                if (mov.CantidadLitros > 0)
                {
                    decimal costoLitro;
                    if (mov.Tipo == "COMPRA" && mov.ReferenciaId.HasValue
                        && costoPorCompra.TryGetValue((mov.ReferenciaId.Value, prodId), out var cc) && cc > 0)
                        costoLitro = cc;
                    else
                        costoLitro = puntero < capas.Count ? capas[puntero].CostoLitro : respaldo;
                    capas.Add(new Capa { Cantidad = mov.CantidadLitros, CostoLitro = costoLitro });
                }
                else
                {
                    var restante = -mov.CantidadLitros;
                    decimal costoConsumido = 0m;
                    while (restante > Eps && puntero < capas.Count)
                    {
                        var capa = capas[puntero];
                        var toma = Math.Min(capa.Cantidad, restante);
                        costoConsumido += toma * capa.CostoLitro;
                        capa.Cantidad -= toma;
                        restante -= toma;
                        if (capa.Cantidad <= Eps) puntero++;
                    }
                    // Stock inicial sin compra registrada (semilla o histórico previo al kardex).
                    if (restante > Eps)
                        costoConsumido += restante * (puntero < capas.Count ? capas[puntero].CostoLitro : respaldo);

                    if (mov.Tipo == "VENTA" && mov.FechaUtc >= ini
                        && (mov.ReferenciaId is null || facturasActivas.Contains(mov.ReferenciaId.Value)))
                    {
                        costoProducto += costoConsumido;
                        litrosVenta += -mov.CantidadLitros;
                    }
                }
            }

            if (litrosVenta > Eps)
            {
                costoProducto = Math.Round(costoProducto, 2);
                total += costoProducto;
                renglones.Add(new CostoVentasRenglonDto(
                    prodId, prod.Sku ?? "", prod.Nombre ?? "", Math.Round(litrosVenta, 2), costoProducto));
            }
        }

        return new CostoVentasFifoDto(suc, desde.Date, hasta.Date, Math.Round(total, 2), renglones);
    }
    public async Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeCostoVentaAsync(
        int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var costo = await CalcularCostoVentasFifoAsync(suc, desde, hasta, ct);
        if (costo.Total <= 0m) return Array.Empty<AsientoLinea>();

        var lineas = new List<AsientoLinea>();
        var reglas = await db.ReglasAsiento.AsNoTracking()
            .Where(r => r.Evento == "CostoVenta" && r.Activo)
            .OrderBy(r => r.Orden).ToListAsync(ct);
        if (reglas.Count >= 2)
        {
            foreach (var r in reglas)
            {
                if (r.MontoOrigen == "iva") continue; // el costo no lleva IVA
                var cuenta = await db.CuentasContables.FindAsync([r.CuentaId], ct);
                if (cuenta is null) continue;
                lineas.Add(new AsientoLinea { CuentaCodigo = cuenta.Codigo, Debe = r.Debe, Importe = costo.Total });
            }
        }
        // Fallback (catálogo sin reglas sembradas): Costo de ventas vs. Almacén.
        if (lineas.Count < 2)
            lineas =
            [
                new() { CuentaCodigo = "5000", Debe = true, Importe = costo.Total },
                new() { CuentaCodigo = "1200", Debe = false, Importe = costo.Total }
            ];
        return lineas.AsReadOnly();
    }

    /// <summary>Rango [desde, hasta] inclusive (local) → [ini, fin) en UTC. Mismo criterio que ReportesFinancierosService.</summary>
    private static (DateTime Ini, DateTime Fin) Rango(DateTime desde, DateTime hasta)
    {
        var ini = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var fin = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        return (ini, fin);
    }
}
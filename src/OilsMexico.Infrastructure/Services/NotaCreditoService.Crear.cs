using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class NotaCreditoService
{
    public async Task<NotaCreditoResult> CrearYTimbrarAsync(CrearNotaCreditoRequest req, CancellationToken ct = default)
    {
        if (req.Motivo is not ("Devolucion" or "Descuento" or "Bonificacion"))
            throw new InvalidOperationException("Motivo NC inválido (Devolucion | Descuento | Bonificacion).");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var f = await db.Facturas.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == req.FacturaOrigenId, ct)
            ?? throw new InvalidOperationException("Factura origen no existe.");
        if (!ctx.EsAdmin && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes acreditar facturas de otra sucursal.");
        if (f.Estado is not (Domain.Enums.EstadoFactura.Timbrada
            or Domain.Enums.EstadoFactura.Surtida or Domain.Enums.EstadoFactura.Entregada))
            throw new InvalidOperationException($"Solo se acredita factura timbrada/surtida/entregada (origen: {f.Estado}).");
        if (f.UuidSat is null) throw new InvalidOperationException("La factura origen no tiene UUID timbrado.");
        // Prioridad 1.1 (ReglaPeriodoCerrado): ninguna NC en mes cerrado; la vía es reabrir (solo Admin).
        // Se consulta en la sucursal de la factura (donde aplica la NC), no en la de sesión.
        var fechaNc = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await _cierreContable.EstaCerradoAsync(f.SucursalId, fechaNc, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(fechaNc));
        var previa = await db.NotasCredito
            .AnyAsync(n => n.FacturaOrigenId == f.Id && n.Estado != EstadoNotaCredito.Cancelada, ct);
        if (previa) throw new InvalidOperationException("La factura ya tiene una NC activa.");

        var nc = new NotaCredito
        {
            SucursalId = f.SucursalId,
            FolioInterno = $"NC-{f.SucursalId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            FacturaOrigenId = f.Id, ClienteId = f.ClienteId,
            UuidFacturaOrigen = f.UuidSat,
            FechaEmision = DateTime.UtcNow,
            Subtotal = f.Subtotal, Iva = f.Iva, Total = f.Total,
            Motivo = req.Motivo, UsoCfdi = "G02", TipoRelacion = "01",
            Estado = EstadoNotaCredito.Pendiente, Observaciones = req.Observaciones?.Trim(),
            UsuarioId = ctx.UsuarioId
        };
        foreach (var d in f.Detalles)
            nc.Detalles.Add(new NotaCreditoDetalle
            {
                ProductoId = d.ProductoId, UnidadNombre = d.UnidadNombre,
                Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario,
                Importe = Math.Round(d.Cantidad * d.PrecioUnitario, 2), LoteId = d.LoteId
            });
        db.NotasCredito.Add(nc);
        await db.SaveChangesAsync(ct);

        // Reposición de stock (una sola vez por NC).
        foreach (var d in f.Detalles)
        {
            var inv = await db.InventarioSucursal.FirstOrDefaultAsync(
                i => i.SucursalId == f.SucursalId && i.ProductoId == d.ProductoId && i.LoteId == d.LoteId, ct);
            if (inv is null)
            {
                inv = new InventarioSucursal
                    { SucursalId = f.SucursalId, ProductoId = d.ProductoId, LoteId = d.LoteId, StockActual = 0 };
                db.InventarioSucursal.Add(inv);
            }
            inv.StockActual += d.LitrosDescontados;
            db.Movimientos.Add(new MovimientoInventario
            {
                SucursalId = f.SucursalId, ProductoId = d.ProductoId, LoteId = d.LoteId,
                CantidadLitros = d.LitrosDescontados, Tipo = "DEVOLUCION",
                Motivo = $"NC {nc.FolioInterno} ({req.Motivo})",
                ReferenciaId = f.Id, UsuarioId = ctx.UsuarioId
            });
        }
        nc.RepusoStock = true;

        await RevertirPolizaVentaAsync(f, nc, ct);

        // CASH BASIS: una NC de este flujo acredita el 100% de la factura (copia los renglones
        // completos), así que la venta queda revertida. Se marca Devolucion para que CobranzaService
        // (comisiones) y EstadoCuentasService dejen de contarla como dinero entrado: sin esto se
        // seguiría pagando comisión sobre mercancía devuelta. Es idempotente (no re-marca al timbrar).
        f.Estado = Domain.Enums.EstadoFactura.Devolucion;
        await db.SaveChangesAsync(ct);

        // Sellado real Tipo E (CSD + XSLT SAT) + timbrado PAC (Finkok o SIMULADO).
        await sellado.SellarNotaCreditoAsync(nc.Id, ct);
        var (uuidNc, xmlNcTimbrado) = await pac.TimbrarAsync(nc.XmlSellado!, ct);
        nc.UuidSat = uuidNc;
        nc.XmlSellado = xmlNcTimbrado;
        nc.Estado = EstadoNotaCredito.Timbrada;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new NotaCreditoResult(nc.Id, nc.FolioInterno, nc.UuidSat,
            nc.Subtotal, nc.Iva, nc.Total, EstadoNotaCredito.Timbrada);
    }

    private async Task RevertirPolizaVentaAsync(Factura factura, NotaCredito nota, CancellationToken ct)
    {
        var conceptoVenta = $"Venta {factura.FolioInterno}";
        var original = await db.AsientosContables
            .Include(a => a.Detalles)
                .ThenInclude(d => d.Cuenta)
            .FirstOrDefaultAsync(a => a.SucursalId == factura.SucursalId
                && a.Concepto == conceptoVenta, ct);
        if (original is null)
            return;

        var detalles = original.Detalles.Select(d => new DetalleAsiento
        {
            CuentaId = d.CuentaId,
            TipoMovimiento = d.TipoMovimiento == MovimientoTipo.Debito
                ? MovimientoTipo.Credito : MovimientoTipo.Debito,
            Importe = d.Importe,
            Descripcion = $"Reversión NC {nota.FolioInterno}"
        }).ToList();
        var reversa = new AsientoContable
        {
            SucursalId = factura.SucursalId,
            UsuarioId = ctx.UsuarioId,
            FechaUtc = nota.FechaEmision,
            Tipo = "Operativo",
            Concepto = $"NC {nota.FolioInterno} reversa {factura.FolioInterno}",
            Detalles = detalles
        };
        db.AsientosContables.Add(reversa);
        foreach (var detalle in detalles)
        {
            var cuenta = original.Detalles.First(d => d.CuentaId == detalle.CuentaId).Cuenta
                ?? throw new InvalidOperationException($"La cuenta contable {detalle.CuentaId} no existe.");
            cuenta.SaldoActual += detalle.TipoMovimiento == MovimientoTipo.Debito
                ? detalle.Importe : -detalle.Importe;
        }
    }
}

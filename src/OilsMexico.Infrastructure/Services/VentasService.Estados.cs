using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class VentasService
{
    public async Task<VentaPosResult> SurtirAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas.FindAsync([facturaId], ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (!ctx.EsAdmin && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes surtir ventas de otra sucursal.");
        // Prioridad 1.1: el surtido mueve inventario (kardex implícito por cambio de estado
        // Surtida); no se acepta en mes cerrado. La vía es reabrir (solo Admin).
        var hoyLocal = ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await _cierreContable.EstaCerradoAsync(f.SucursalId, hoyLocal, ct))
            throw new InvalidOperationException(ReglaPeriodoCerrado.Mensaje(hoyLocal));
        f.Estado = EstadoFactura.Surtida;
        await db.SaveChangesAsync(ct);
        return new VentaPosResult(f.Id, f.FolioInterno, f.UuidSat,
            f.Subtotal, f.Iva, f.Total, EstadoFactura.Surtida);
    }

    public async Task<VentaPosResult> DevolverAsync(int facturaId, string motivo, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var f = await db.Facturas.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (!ctx.EsAdmin && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes devolver ventas de otra sucursal.");
        // Prioridad 1.1: la devolución repone stock + kardex DEVOLUCION; no se acepta en mes cerrado.
        var hoyLocal = ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await _cierreContable.EstaCerradoAsync(f.SucursalId, hoyLocal, ct))
            throw new InvalidOperationException(ReglaPeriodoCerrado.Mensaje(hoyLocal));
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
                Motivo = motivo, ReferenciaId = f.Id, UsuarioId = ctx.UsuarioId
            });
        }
        f.Estado = EstadoFactura.Devolucion;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new VentaPosResult(f.Id, f.FolioInterno, f.UuidSat,
            f.Subtotal, f.Iva, f.Total, EstadoFactura.Devolucion);
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class VentasService
{
    public async Task<VentaPosResult> SurtirAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas.FindAsync([facturaId], ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        f.Estado = EstadoFactura.Surtida;
        await db.SaveChangesAsync(ct);
        return new VentaPosResult(f.Id, f.FolioInterno, f.UuidSat,
            f.Subtotal, f.Iva, f.Total, "SURTIDA");
    }

    public async Task<VentaPosResult> DevolverAsync(int facturaId, string motivo, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var f = await db.Facturas.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (f.SucursalId != ctx.SucursalId && ctx.Rol != "Admin")
            throw new UnauthorizedAccessException("No puedes devolver ventas de otra sucursal.");
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
            f.Subtotal, f.Iva, f.Total, "DEVOLUCION");
    }
}

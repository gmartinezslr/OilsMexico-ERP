using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Compras ciclo completo: orden → recepción (stock + kardex) → CxP (pagos).</summary>
public sealed partial class ComprasService(ErpDbContext db, ISucursalContext ctx) : IComprasService
{
    private const decimal TasaIva = 0.16m;

    public async Task<CompraResult> CrearOrdenAsync(CrearCompraRequest req, CancellationToken ct = default)
    {
        var sucursalId = ctx.Rol == "Admin" ? req.SucursalId : ctx.SucursalId;
        if (req.Renglones.Count == 0) throw new InvalidOperationException("La orden no tiene renglones.");
        var prov = await db.Proveedores.FindAsync([req.ProveedorId], ct)
            ?? throw new InvalidOperationException("Proveedor no existe.");
        if (!prov.Activo) throw new InvalidOperationException("El proveedor está inactivo.");

        var compra = new Compra
        {
            SucursalId = sucursalId,
            FolioInterno = $"C-{sucursalId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ProveedorId = prov.Id,
            FolioProveedor = string.IsNullOrWhiteSpace(req.FolioProveedor) ? null : req.FolioProveedor.Trim(),
            Notas = req.Notas, Estado = "Borrador", EstadoPago = "Pendiente",
            UsuarioId = ctx.UsuarioId
        };
        decimal subtotal = 0m;
        foreach (var r in req.Renglones)
        {
            if (r.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
            if (r.CostoUnitario < 0) throw new InvalidOperationException("El costo no puede ser negativo.");
            var prod = await db.Productos.FindAsync([r.ProductoId], ct)
                ?? throw new InvalidOperationException("Producto no existe.");
            if (!prod.Activo) throw new InvalidOperationException("El producto está inactivo.");
            var factor = r.FactorConversion > 0 ? r.FactorConversion : 1m;
            compra.Detalles.Add(new CompraDetalle
            {
                ProductoId = r.ProductoId, UnidadMedidaId = r.UnidadMedidaId,
                UnidadNombre = r.UnidadNombre, Cantidad = r.Cantidad,
                FactorConversion = factor, CostoUnitario = r.CostoUnitario
            });
            subtotal += r.Cantidad * r.CostoUnitario;
        }
        compra.Subtotal = Math.Round(subtotal, 2);
        compra.Iva = Math.Round(subtotal * TasaIva, 2);
        compra.Total = Math.Round(compra.Subtotal + compra.Iva, 2);
        db.Compras.Add(compra);
        await db.SaveChangesAsync(ct);
        return new CompraResult(compra.Id, compra.FolioInterno, compra.Subtotal, compra.Iva, compra.Total, compra.Estado);
    }

    public async Task<CompraResult> RecibirAsync(RecepcionRequest req, CancellationToken ct = default)
    {
        var compra = await db.Compras.Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == req.CompraId, ct)
            ?? throw new InvalidOperationException("Compra no existe.");
        if (ctx.Rol != "Admin" && compra.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes recibir compras de otra sucursal.");
        if (compra.Estado == "Cancelada") throw new InvalidOperationException("La compra está cancelada.");
        if (compra.Estado == "Recibida") throw new InvalidOperationException("La compra ya fue recibida.");
        if (req.Renglones.Count == 0) throw new InvalidOperationException("Nada que recibir.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var r in req.Renglones)
        {
            if (r.CantidadRecibir <= 0) continue;
            var det = compra.Detalles.FirstOrDefault(d => d.Id == r.DetalleId)
                ?? throw new InvalidOperationException("Renglón no pertenece a la compra.");
            var pendiente = det.Cantidad - det.CantidadRecibida;
            if (r.CantidadRecibir - pendiente > 0.0001m)
                throw new InvalidOperationException("No puedes recibir más de lo pendiente.");
            var litros = r.CantidadRecibir * det.FactorConversion;

            int? loteId = null;
            if (!string.IsNullOrWhiteSpace(r.NumeroLote))
            {
                var numLote = r.NumeroLote.Trim();
                var lote = await db.Lotes.FirstOrDefaultAsync(
                    l => l.ProductoId == det.ProductoId && l.NumeroLote == numLote, ct);
                if (lote is null)
                {
                    lote = new InventarioLote
                    {
                        ProductoId = det.ProductoId, NumeroLote = numLote,
                        FechaFabricacion = DateOnly.FromDateTime(DateTime.UtcNow),
                        FechaCaducidad = r.FechaCaducidad, CantidadDisponible = 0
                    };
                    db.Lotes.Add(lote);
                    await db.SaveChangesAsync(ct);
                }
                lote.CantidadDisponible += litros;
                if (r.FechaCaducidad.HasValue) lote.FechaCaducidad = r.FechaCaducidad;
                loteId = lote.Id;
                det.NumeroLote ??= lote.NumeroLote;
                det.FechaCaducidad ??= r.FechaCaducidad;
            }
            var inv = await db.InventarioSucursal.FirstOrDefaultAsync(
                i => i.SucursalId == compra.SucursalId && i.ProductoId == det.ProductoId && i.LoteId == loteId, ct);
            if (inv is null)
            {
                inv = new InventarioSucursal
                { SucursalId = compra.SucursalId, ProductoId = det.ProductoId, LoteId = loteId, StockMinimo = 5 };
                db.InventarioSucursal.Add(inv);
            }
            inv.StockActual += litros;
            inv.ActualizadoUtc = DateTime.UtcNow;
            det.CantidadRecibida += r.CantidadRecibir;
            det.LitrosRecibidos += litros;
            db.Movimientos.Add(new MovimientoInventario
            {
                SucursalId = compra.SucursalId, ProductoId = det.ProductoId, LoteId = loteId,
                CantidadLitros = litros, Tipo = "COMPRA", UsuarioId = ctx.UsuarioId,
                ReferenciaId = compra.Id,
                Motivo = $"Recepción {compra.FolioInterno} ({r.CantidadRecibir} {det.UnidadNombre})"
            });
        }
        await db.SaveChangesAsync(ct);
        compra.Estado = compra.Detalles.All(d => d.CantidadRecibida + 0.0001m >= d.Cantidad)
            ? "Recibida" : "Parcial";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CompraResult(compra.Id, compra.FolioInterno, compra.Subtotal, compra.Iva, compra.Total, compra.Estado);
    }
}

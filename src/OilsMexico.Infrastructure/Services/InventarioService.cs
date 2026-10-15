using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class InventarioService(ErpDbContext db, ISucursalContext ctx, ICierreContableService cierre) : IInventarioService
{
    public async Task EntradaCompraAsync(int sucursalId, int productoId, int? loteId,
        decimal litros, int usuarioId, string? motivo = null, CancellationToken ct = default)
    {
        ValidarSucursal(sucursalId);
        ValidarActor(usuarioId);
        if (litros <= 0) throw new InvalidOperationException("Los litros deben ser mayores a cero.");
        var fechaLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await cierre.EstaCerradoAsync(sucursalId, fechaLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(fechaLocal));
        var inv = await BuscarOCrear(sucursalId, productoId, loteId, ct);
        inv.StockActual += litros;
        inv.ActualizadoUtc = DateTime.UtcNow;
        db.Movimientos.Add(new MovimientoInventario
        {
            SucursalId = sucursalId, ProductoId = productoId, LoteId = loteId,
            CantidadLitros = litros, Tipo = "COMPRA", Motivo = motivo,
            UsuarioId = usuarioId
        });
        RegistrarAuditoria("EntradaAlmacen",
            $"Entrada prod #{productoId} suc {sucursalId}: +{litros:N2} L. Motivo: {motivo}",
            $"{inv.StockActual - litros:N2}", $"{inv.StockActual:N2}");
        await db.SaveChangesAsync(ct);
    }

    public async Task AjusteAsync(int sucursalId, int productoId, int? loteId,
        decimal stockRealLitros, int usuarioId, string motivo, CancellationToken ct = default)
    {
        ValidarSucursal(sucursalId);
        ValidarActor(usuarioId);
        if (stockRealLitros < 0) throw new InvalidOperationException("El stock real no puede ser negativo.");
        // Prioridad 1.1: el ajuste mueve kardex AJUSTE (alimenta FIFO); no se acepta en mes cerrado.
        var hoyLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await cierre.EstaCerradoAsync(sucursalId, hoyLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(hoyLocal));
        var inv = await BuscarOCrear(sucursalId, productoId, loteId, ct);
        var antes = inv.StockActual;
        var dif = stockRealLitros - inv.StockActual;
        inv.StockActual = stockRealLitros;
        inv.ActualizadoUtc = DateTime.UtcNow;
        db.Movimientos.Add(new MovimientoInventario
        {
            SucursalId = sucursalId, ProductoId = productoId, LoteId = loteId,
            CantidadLitros = dif, Tipo = "AJUSTE", Motivo = motivo, UsuarioId = usuarioId
        });
        // Prioridad 1.2: trazabilidad de ajuste de almacén (quién/cuándo/antes/después).
        RegistrarAuditoria("AjusteAlmacen",
            $"Ajuste prod #{productoId} suc {sucursalId}: {antes:N2} → {stockRealLitros:N2} L. Motivo: {motivo}",
            $"{antes:N2}", $"{stockRealLitros:N2}");
        await db.SaveChangesAsync(ct);
    }

    public async Task TraspasoAsync(int origenId, int destinoId, int productoId,
        decimal litros, int usuarioId, CancellationToken ct = default)
    {
        ValidarActor(usuarioId);
        if (litros <= 0) throw new InvalidOperationException("Los litros deben ser mayores a cero.");
        if (origenId == destinoId) throw new InvalidOperationException("El origen y destino deben ser sucursales distintas.");
        if (!ctx.EsAdmin && ctx.SucursalId != origenId)
            throw new UnauthorizedAccessException("Traspaso no autorizado.");
        // Prioridad 1.1: el traspaso mueve kardex OUT/IN en ambas sucursales; si CUALQUIERA
        // está cerrada se rechaza (no se deja medio traspaso).
        var hoyLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await cierre.EstaCerradoAsync(origenId, hoyLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(hoyLocal));
        if (destinoId != origenId && await cierre.EstaCerradoAsync(destinoId, hoyLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(hoyLocal));
        var origen = await BuscarOCrear(origenId, productoId, null, ct);
        if (origen.StockActual < litros)
            throw new InvalidOperationException("Stock insuficiente para traspaso.");
        var stockOrigenAntes = origen.StockActual;
        origen.StockActual -= litros;
        var destino = await BuscarOCrear(destinoId, productoId, null, ct);
        var stockDestinoAntes = destino.StockActual;
        destino.StockActual += litros;
        db.Movimientos.Add(new MovimientoInventario
        {
            SucursalId = origenId, ProductoId = productoId, CantidadLitros = -litros,
            Tipo = "TRASPASO_OUT", UsuarioId = usuarioId
        });
        db.Movimientos.Add(new MovimientoInventario
        {
            SucursalId = destinoId, ProductoId = productoId, CantidadLitros = litros,
            Tipo = "TRASPASO_IN", UsuarioId = usuarioId
        });
        // Prioridad 1.2: trazabilidad de traspaso.
        RegistrarAuditoria("TraspasoAlmacen",
            $"Traspaso prod #{productoId}: {litros:N2} L de suc {origenId} → suc {destinoId}.",
            $"Origen={stockOrigenAntes:N2}; Destino={stockDestinoAntes:N2}",
            $"Origen={origen.StockActual:N2}; Destino={destino.StockActual:N2}");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Agrega la auditoría a la misma unidad de trabajo que el movimiento.</summary>
    private void RegistrarAuditoria(string tipo, string? detalle, string? anterior, string? nuevo)
    {
        if (!ctx.Establecida || ctx.UsuarioId <= 0)
            throw new InvalidOperationException("No se puede modificar el inventario sin un usuario autenticado.");
        db.AuditLogs.Add(new AuditLog
        {
            UsuarioId = ctx.UsuarioId,
            Tipo = tipo,
            Detalle = detalle,
            Anterior = anterior,
            Nuevo = nuevo,
            FechaUtc = DateTime.UtcNow
        });
    }

    private void ValidarSucursal(int sucursalId)
    {
        if (!ctx.EsAdmin && ctx.SucursalId != sucursalId)
            throw new UnauthorizedAccessException("Operación fuera de tu sucursal.");
    }

    private void ValidarActor(int usuarioId)
    {
        if (!ctx.Establecida || ctx.UsuarioId <= 0 || usuarioId != ctx.UsuarioId)
            throw new UnauthorizedAccessException("El usuario de la operación no coincide con la sesión autenticada.");
    }

    private async Task<InventarioSucursal> BuscarOCrear(int suc, int prod, int? lote, CancellationToken ct)
    {
        var inv = await db.InventarioSucursal.FirstOrDefaultAsync(
            i => i.SucursalId == suc && i.ProductoId == prod && i.LoteId == lote, ct);
        if (inv is null)
        {
            inv = new InventarioSucursal { SucursalId = suc, ProductoId = prod, LoteId = lote };
            db.InventarioSucursal.Add(inv);
        }
        return inv;
    }
}

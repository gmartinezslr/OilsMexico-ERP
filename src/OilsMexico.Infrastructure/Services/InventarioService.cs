using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class InventarioService(ErpDbContext db, ISucursalContext ctx) : IInventarioService
{
    public async Task EntradaCompraAsync(int sucursalId, int productoId, int? loteId,
        decimal litros, int usuarioId, string? motivo = null, CancellationToken ct = default)
    {
        ValidarSucursal(sucursalId);
        var inv = await BuscarOCrear(sucursalId, productoId, loteId, ct);
        inv.StockActual += litros;
        inv.ActualizadoUtc = DateTime.UtcNow;
        db.Movimientos.Add(new MovimientoInventario
        {
            SucursalId = sucursalId, ProductoId = productoId, LoteId = loteId,
            CantidadLitros = litros, Tipo = "COMPRA", Motivo = motivo,
            UsuarioId = usuarioId
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task AjusteAsync(int sucursalId, int productoId, int? loteId,
        decimal stockRealLitros, int usuarioId, string motivo, CancellationToken ct = default)
    {
        ValidarSucursal(sucursalId);
        var inv = await BuscarOCrear(sucursalId, productoId, loteId, ct);
        var dif = stockRealLitros - inv.StockActual;
        inv.StockActual = stockRealLitros;
        inv.ActualizadoUtc = DateTime.UtcNow;
        db.Movimientos.Add(new MovimientoInventario
        {
            SucursalId = sucursalId, ProductoId = productoId, LoteId = loteId,
            CantidadLitros = dif, Tipo = "AJUSTE", Motivo = motivo, UsuarioId = usuarioId
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task TraspasoAsync(int origenId, int destinoId, int productoId,
        decimal litros, int usuarioId, CancellationToken ct = default)
    {
        if (ctx.Rol != "Admin" && ctx.SucursalId != origenId)
            throw new UnauthorizedAccessException("Traspaso no autorizado.");
        var origen = await BuscarOCrear(origenId, productoId, null, ct);
        if (origen.StockActual < litros)
            throw new InvalidOperationException("Stock insuficiente para traspaso.");
        origen.StockActual -= litros;
        var destino = await BuscarOCrear(destinoId, productoId, null, ct);
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
        await db.SaveChangesAsync(ct);
    }

    private void ValidarSucursal(int sucursalId)
    {
        if (ctx.Rol != "Admin" && ctx.SucursalId != sucursalId)
            throw new UnauthorizedAccessException("Operación fuera de tu sucursal.");
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

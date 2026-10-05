using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class AlmacenConsultaService(ErpDbContext db) : IAlmacenConsulta
{
    public async Task<List<AlmacenStockDto>> StockPorSucursalAsync(
        int sucursalId, string? filtro, CancellationToken ct = default)
    {
        var q = from i in db.InventarioSucursal
                join p in db.Productos on i.ProductoId equals p.Id
                join l in db.Lotes on i.LoteId equals l.Id into ls
                from lote in ls.DefaultIfEmpty()
                where i.SucursalId == sucursalId
                select new { i, p, Lote = lote != null ? lote.NumeroLote : null };

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(x => x.p.Sku.ToLower().Contains(f) || x.p.Nombre.ToLower().Contains(f));
        }

        return await q.Select(x => new AlmacenStockDto(
            x.p.Id, x.p.Sku, x.p.Nombre, x.p.Marca, x.p.Viscosidad,
            x.i.LoteId, x.Lote, x.i.StockActual, x.i.StockMinimo,
            x.i.StockActual <= x.i.StockMinimo))
            .OrderBy(x => x.Nombre).Take(200).ToListAsync(ct);
    }

    public async Task<List<KardexDto>> KardexAsync(
        int sucursalId, int productoId, int dias = 30, CancellationToken ct = default)
    {
        var desde = DateTime.UtcNow.AddDays(-dias);
        var movs = await db.Movimientos
            .Where(m => m.SucursalId == sucursalId && m.ProductoId == productoId && m.FechaUtc >= desde)
            .OrderByDescending(m => m.FechaUtc).Take(200).ToListAsync(ct);
        var usuarios = await db.Usuarios.ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        return movs.Select(m => new KardexDto(m.FechaUtc, m.Tipo, m.CantidadLitros,
            m.Motivo, usuarios.TryGetValue(m.UsuarioId, out var n) ? n : $"#{m.UsuarioId}",
            m.ReferenciaId)).ToList();
    }
}

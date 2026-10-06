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

        // OrderBy ANTES del Select: ordenar sobre el DTO ya construido no es traducible a SQL.
        return await q.OrderBy(x => x.p.Nombre)
            .Select(x => new AlmacenStockDto(
                x.p.Id, x.p.Sku, x.p.Nombre, x.p.Marca, x.p.Viscosidad,
                x.i.LoteId, x.Lote, x.i.StockActual, x.i.StockMinimo,
                x.i.StockActual <= x.i.StockMinimo))
            .Take(200).ToListAsync(ct);
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

    public async Task<List<ViscosidadProductoDto>> ViscosidadAsync(
        int sucursalId, string? filtro = null, CancellationToken ct = default)
    {
        var q = db.Productos.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(p => p.Sku.ToLower().Contains(f)
                || p.Nombre.ToLower().Contains(f)
                || p.Marca.ToLower().Contains(f)
                || p.Viscosidad.ToLower().Contains(f));
        }
        var productos = await q
            .OrderBy(p => p.Viscosidad).ThenBy(p => p.Nombre)
            .Take(500).ToListAsync(ct);
        if (productos.Count == 0) return [];

        var ids = productos.Select(p => p.Id).ToList();
        // Stock y mínimo agregados por producto (un producto puede tener varios lotes).
        var stock = await db.InventarioSucursal
            .Where(i => i.SucursalId == sucursalId && ids.Contains(i.ProductoId))
            .GroupBy(i => i.ProductoId)
            .Select(g => new { g.Key, Stock = g.Sum(i => i.StockActual), Minimo = g.Sum(i => i.StockMinimo) })
            .ToDictionaryAsync(g => g.Key, g => g, ct);

        return productos.Select(p =>
        {
            stock.TryGetValue(p.Id, out var s);
            var litros = s?.Stock ?? 0m;
            var minimo = s?.Minimo ?? 0m;
            return new ViscosidadProductoDto(
                p.Id, p.Sku, p.Nombre, p.Marca, p.TipoBase, p.Viscosidad,
                litros, minimo, p.PrecioVenta,
                minimo > 0 && litros <= minimo);
        }).ToList();
    }
}

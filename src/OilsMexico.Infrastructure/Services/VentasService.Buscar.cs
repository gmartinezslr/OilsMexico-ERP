using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class VentasService(
    ErpDbContext db, ISucursalContext ctx,
    ICfdiSelladoService sellado, IPacTimbradoService pac,
    ICierreContableService cierreContable, ICobranzaService cobranza) : IVentasService
{
    // §1.3 (periodo cerrado) y §4.1 (crédito): se usan en RegistrarVentaAsync (Venta.cs).
    private readonly ICierreContableService _cierreContable = cierreContable;
    private readonly ICobranzaService _cobranza = cobranza;

    public async Task<List<ProductoDto>> BuscarProductosAsync(int sucursalId, string? filtro, CancellationToken ct = default)
    {
        // Si el filtro es un código de barras exacto, se devuelve el renglón de la
        // PRESENTACIÓN escaneada (no la unidad default): el POS la agrega directo.
        var codigo = CodigosBarra.Normalizar(filtro);
        if (!string.IsNullOrEmpty(codigo))
        {
            var exacto = await (from u in db.UnidadesMedida.AsNoTracking()
                                join p in db.Productos.AsNoTracking() on u.ProductoId equals p.Id
                                where p.Activo && u.CodigoBarra != null && u.CodigoBarra == codigo
                                select new { p, u }).FirstOrDefaultAsync(ct);
            if (exacto is not null)
            {
                var stockExacto = await db.InventarioSucursal.AsNoTracking()
                    .Where(i => i.SucursalId == sucursalId && i.ProductoId == exacto.p.Id)
                    .SumAsync(i => (decimal?)i.StockActual, ct) ?? 0m;
                return
                [
                    new ProductoDto(exacto.p.Id, exacto.p.Sku, exacto.p.Nombre, exacto.p.Marca,
                        exacto.p.Viscosidad, exacto.p.TipoBase, exacto.p.PrecioVenta, exacto.p.PrecioMayoreo,
                        stockExacto, exacto.u.UnidadNombre, exacto.u.FactorConversion,
                        exacto.u.Id, exacto.u.CodigoBarra)
                ];
            }
        }

        // Un renglon por producto: unidad default = Litro (o la primera) y stock sumado por sucursal.
        var q = from p in db.Productos
                where p.Activo
                select new
                {
                    p,
                    Unidad = db.UnidadesMedida
                        .Where(u => u.ProductoId == p.Id)
                        .OrderBy(u => u.UnidadNombre != "Litro").ThenBy(u => u.Id)
                        .FirstOrDefault(),
                    Stock = db.InventarioSucursal
                        .Where(i => i.SucursalId == sucursalId && i.ProductoId == p.Id)
                        .Sum(i => (decimal?)i.StockActual) ?? 0m
                };

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(x => x.p.Sku.ToLower().Contains(f)
                || (x.p.SkuAnterior != null && x.p.SkuAnterior.ToLower().Contains(f))
                || x.p.Nombre.ToLower().Contains(f)
                || x.p.Marca.ToLower().Contains(f)
                || x.p.Viscosidad.ToLower().Contains(f)
                || (x.p.Categoria != null && x.p.Categoria.ToLower().Contains(f))
                || (x.p.Especificacion != null && x.p.Especificacion.ToLower().Contains(f))
                || db.UnidadesMedida.Any(u => u.ProductoId == x.p.Id
                    && u.CodigoBarra != null && u.CodigoBarra.ToLower().Contains(f)));
        }

        return await q.Select(x => new ProductoDto(
            x.p.Id, x.p.Sku, x.p.Nombre, x.p.Marca, x.p.Viscosidad, x.p.TipoBase,
            x.p.PrecioVenta, x.p.PrecioMayoreo, x.Stock,
            x.Unidad != null ? x.Unidad.UnidadNombre : "Litro",
            x.Unidad != null ? x.Unidad.FactorConversion : 1m,
            x.Unidad != null ? x.Unidad.Id : 0,
            x.Unidad != null ? x.Unidad.CodigoBarra : null))
            .Take(60).ToListAsync(ct);
    }
}

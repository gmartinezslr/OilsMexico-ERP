using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class VentasService(
    ErpDbContext db, ISucursalContext ctx,
    ICfdiSelladoService sellado, IPacTimbradoService pac) : IVentasService
{

    public async Task<List<ProductoDto>> BuscarProductosAsync(int sucursalId, string? filtro, CancellationToken ct = default)
    {
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
                || (x.p.Especificacion != null && x.p.Especificacion.ToLower().Contains(f)));
        }

        return await q.Select(x => new ProductoDto(
            x.p.Id, x.p.Sku, x.p.Nombre, x.p.Marca, x.p.Viscosidad, x.p.TipoBase,
            x.p.PrecioVenta, x.p.PrecioMayoreo, x.Stock,
            x.Unidad != null ? x.Unidad.UnidadNombre : "Litro",
            x.Unidad != null ? x.Unidad.FactorConversion : 1m))
            .Take(60).ToListAsync(ct);
    }
}

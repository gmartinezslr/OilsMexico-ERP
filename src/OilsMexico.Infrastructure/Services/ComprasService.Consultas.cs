using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class ComprasService
{
    public async Task<List<CompraListadoDto>> ListarAsync(
        int sucursalId, EstadoCompra? estado, string? texto, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var q = db.Compras.AsNoTracking().Where(c => c.SucursalId == suc);
        if (estado is not null) q = q.Where(c => c.Estado == estado);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(c => c.FolioInterno.ToLower().Contains(t)
                || (c.FolioProveedor != null && c.FolioProveedor.ToLower().Contains(t))
                || (c.Proveedor != null && c.Proveedor.Nombre.ToLower().Contains(t)));
        }
        var filas = await q.OrderByDescending(c => c.FechaEmision).Take(200)
            .Select(c => new
            {
                c.Id, c.FolioInterno, c.FechaEmision, c.FolioProveedor, c.Estado, c.EstadoPago,
                c.Subtotal, c.Iva, c.Total, c.MontoPagado,
                Proveedor = c.Proveedor != null ? c.Proveedor.Nombre : "(sin proveedor)",
                Renglones = c.Detalles.Count,
                Pedido = c.Detalles.Sum(d => d.Cantidad * d.FactorConversion),
                Recibido = c.Detalles.Sum(d => d.LitrosRecibidos)
            }).ToListAsync(ct);
        return filas.Select(f => new CompraListadoDto(
            f.Id, f.FolioInterno, f.FechaEmision, f.Proveedor, f.FolioProveedor,
            f.Estado, f.EstadoPago, f.Subtotal, f.Iva, f.Total, f.MontoPagado,
            f.Renglones, Math.Round(f.Total - f.MontoPagado, 2))
        { PorcentajeRecibido = f.Pedido > 0 ? Math.Round(f.Recibido / f.Pedido * 100, 1) : 0 }).ToList();
    }

    public async Task<CompraDetalleDto?> DetalleAsync(int compraId, CancellationToken ct = default)
    {
        var c = await db.Compras.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(x => x.Id == compraId, ct);
        if (c is null) return null;
        if (ctx.Rol != "Admin" && c.SucursalId != ctx.SucursalId) return null;
        var pagos = await (from p in db.CompraPagos.AsNoTracking()
                           join u in db.Usuarios.AsNoTracking() on p.UsuarioId equals u.Id into uj
                           from u in uj.DefaultIfEmpty()
                           where p.CompraId == compraId
                           orderby p.FechaUtc
                           select new CompraPagoDto(p.Id, p.FechaUtc, p.Monto, p.FormaPago, p.Referencia,
                               u != null ? u.Nombre : "#" + p.UsuarioId)).ToListAsync(ct);
        var prov = await db.Proveedores.AsNoTracking()
            .Where(p => p.Id == c.ProveedorId).Select(p => p.Nombre).FirstOrDefaultAsync(ct);
        return new CompraDetalleDto(c.Id, c.FolioInterno, c.FechaEmision, prov ?? "(sin proveedor)",
            c.FolioProveedor, c.Estado, c.EstadoPago, c.Subtotal, c.Iva, c.Total,
            c.MontoPagado, Math.Round(c.Total - c.MontoPagado, 2), c.Notas,
            c.Detalles.Select(d => new CompraDetalleLineaDto(d.Id,
                d.Producto != null ? d.Producto.Nombre : "#" + d.ProductoId,
                d.Producto != null ? d.Producto.Sku : "",
                d.UnidadNombre, d.Cantidad, d.CantidadRecibida, d.CostoUnitario,
                Math.Round(d.Cantidad * d.CostoUnitario, 2), d.NumeroLote, d.FechaCaducidad)).ToList(),
            pagos);
    }

    public async Task<List<CuentasPorPagarDto>> CuentasPorPagarAsync(
        int sucursalId, string? texto, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var q = db.Compras.AsNoTracking()
            .Where(c => c.SucursalId == suc && c.Estado != EstadoCompra.Cancelada && c.Total - c.MontoPagado > 0.01m);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(c => c.FolioInterno.ToLower().Contains(t)
                || (c.FolioProveedor != null && c.FolioProveedor.ToLower().Contains(t))
                || (c.Proveedor != null && c.Proveedor.Nombre.ToLower().Contains(t)));
        }
        return await q.OrderBy(c => c.FechaEmision).Take(200)
            .Select(c => new CuentasPorPagarDto(c.Id, c.FolioInterno, c.FechaEmision,
                c.Proveedor != null ? c.Proveedor.Nombre : "(sin proveedor)",
                c.FolioProveedor, c.Estado, c.Total, c.MontoPagado,
                Math.Round(c.Total - c.MontoPagado, 2))).ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CotizacionesService
{
    public async Task<List<CotizacionListadoDto>> ListarCotizacionesAsync(
        int sucursalId, EstadoCotizacion? estado, string? clienteTexto,
        DateTime? desde, DateTime? hasta, int pagina, int porPagina, CancellationToken ct = default)
    {
        IQueryable<Cotizacion> query = db.Cotizaciones
            .Where(c => c.SucursalId == sucursalId)
            .OrderByDescending(c => c.FechaEmision);

        if (estado.HasValue) query = query.Where(c => c.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(clienteTexto))
            query = query.Where(c => c.Cliente!.Nombre.Contains(clienteTexto.Trim()));
        if (desde.HasValue) query = query.Where(c => c.FechaEmision >= desde.Value);
        if (hasta.HasValue) query = query.Where(c => c.FechaEmision <= hasta.Value);

        var total = await query.CountAsync(ct);
        var paginaActual = Math.Max(1, pagina);
        var porPaginaReal = Math.Clamp(porPagina, 1, 100);

        var registros = await query
            .Skip((paginaActual - 1) * porPaginaReal)
            .Take(porPaginaReal)
            .Include(c => c.Cliente)
            .Include(c => c.Vendedor)
            .Include(c => c.Detalles)
            .ToListAsync(ct);

        return registros.Select(MapearACotizacionListadoDto).ToList();
    }

    public async Task<CotizacionResponseDto?> ObtenerCotizacionAsync(int id, CancellationToken ct = default)
    {
        // Se mapea en memoria: los totales (Subtotal/Iva/Total) se calculan en C# y no son traducibles a SQL.
        var c = await db.Cotizaciones
            .Where(c => c.Id == id)
            .Include(c => c.Sucursal)
            .Include(c => c.Cliente)
            .Include(c => c.Vendedor)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(ct);

        if (c is null) return null;

        var totales = CalcularTotales(c.Detalles);
        return new CotizacionResponseDto(
            c.Id, c.SucursalId, c.Sucursal?.Nombre ?? string.Empty, c.ClienteId, c.Cliente?.Nombre ?? string.Empty,
            c.VendedorId, c.Vendedor?.Nombre, c.FechaEmision, c.FechaVencimiento,
            c.FolioInterno, c.Notas, c.Estado,
            c.Detalles.Select(MapearACotizacionRenglonDto).ToList(),
            totales.Subtotal, totales.Iva, totales.Total);
    }

    private static CotizacionRenglonDto MapearACotizacionRenglonDto(CotizacionDetalle d) =>
        new(d.Id, d.ProductoId, d.Producto?.Sku ?? string.Empty, d.Producto?.Nombre ?? string.Empty,
            d.UnidadMedidaId, d.UnidadNombre, d.Cantidad, d.PrecioUnitario, d.Descuento, d.Importe);

    public async Task<List<OrdenVentaListadoDto>> ListarOrdenesVentaAsync(
        int sucursalId, EstadoOrdenVenta? estado, string? clienteTexto,
        DateTime? desde, DateTime? hasta, int pagina, int porPagina, CancellationToken ct = default)
    {
        IQueryable<OrdenVenta> query = db.OrdenesVenta
            .Where(o => o.SucursalId == sucursalId)
            .OrderByDescending(o => o.FechaEmision);

        if (estado.HasValue) query = query.Where(o => o.Estado == estado.Value);
        if (!string.IsNullOrWhiteSpace(clienteTexto))
            query = query.Where(o => o.Cliente!.Nombre.Contains(clienteTexto.Trim()));
        if (desde.HasValue) query = query.Where(o => o.FechaEmision >= desde.Value);
        if (hasta.HasValue) query = query.Where(o => o.FechaEmision <= hasta.Value);

        var total = await query.CountAsync(ct);
        var paginaActual = Math.Max(1, pagina);
        var porPaginaReal = Math.Clamp(porPagina, 1, 100);

        var registros = await query
            .Skip((paginaActual - 1) * porPaginaReal)
            .Take(porPaginaReal)
            .Include(o => o.Cliente)
            .Include(o => o.Vendedor)
            .Include(o => o.Detalles)
            .ToListAsync(ct);

        return registros.Select(MapearAOrdenVentaListadoDto).ToList();
    }

    public async Task<OrdenVentaDetalleDto?> ObtenerOrdenVentaAsync(int id, CancellationToken ct = default)
    {
        // Se mapea en memoria: los totales se calculan en C# (no traducibles a SQL).
        var o = await db.OrdenesVenta
            .Where(o => o.Id == id)
            .Include(o => o.Sucursal)
            .Include(o => o.Cliente)
            .Include(o => o.Vendedor)
            .Include(o => o.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(ct);

        if (o is null) return null;

        var totales = CalcularTotalesOrden(o.Detalles);
        return new OrdenVentaDetalleDto(
            o.Id, o.SucursalId, o.Sucursal?.Nombre ?? string.Empty, o.ClienteId, o.Cliente?.Nombre ?? string.Empty,
            o.VendedorId, o.Vendedor?.Nombre, o.FechaEmision, o.FolioInterno, o.Notas, o.Estado,
            o.Detalles.Select(MapearAOrdenVentaLineaDto).ToList(),
            totales.Subtotal, totales.Iva, totales.Total, o.CotizacionId ?? 0, o.FacturaId);
    }

    private static OrdenVentaDetalleLineaDto MapearAOrdenVentaLineaDto(OrdenVentaDetalle d) =>
        new(d.Id, d.ProductoId, d.Producto?.Sku ?? string.Empty, d.Producto?.Nombre ?? string.Empty,
            d.UnidadMedidaId, d.UnidadNombre, d.Cantidad, d.PrecioUnitario, d.Descuento, d.Importe);

    private static CotizacionListadoDto MapearACotizacionListadoDto(Cotizacion c) =>
        new(c.Id, c.FolioInterno ?? string.Empty, c.FechaEmision, c.Cliente!.Nombre, c.Vendedor?.Nombre, c.Estado, c.Detalles.Count,
            CalcularTotales(c.Detalles).Subtotal, CalcularTotales(c.Detalles).Iva, CalcularTotales(c.Detalles).Total);

    private static OrdenVentaListadoDto MapearAOrdenVentaListadoDto(OrdenVenta o) =>
        new(o.Id, o.FolioInterno ?? string.Empty, o.FechaEmision, o.Cliente!.Nombre, o.Vendedor?.Nombre, o.Estado, o.Detalles.Count,
            CalcularTotalesOrden(o.Detalles).Subtotal, CalcularTotalesOrden(o.Detalles).Iva, CalcularTotalesOrden(o.Detalles).Total,
            o.CotizacionId > 0 ? o.CotizacionId : null);
}

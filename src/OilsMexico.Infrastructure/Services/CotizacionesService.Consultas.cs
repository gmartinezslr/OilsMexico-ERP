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
        // REGLA #1 en lecturas (§6.2): un rol no-Admin sólo ve cotizaciones de SU sucursal;
        // el resultado para otra sucursal es null, igual que si no existiera.
        // Se mapea en memoria: los totales (Subtotal/Iva/Total) se calculan en C# y no son traducibles a SQL.
        IQueryable<Cotizacion> q = db.Cotizaciones;
        if (!ctx.EsAdmin) q = q.Where(c => c.SucursalId == ctx.SucursalId);
        var c = await q
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
            totales.Subtotal, totales.Iva, totales.Total,
            c.Cliente?.Email);
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
        // REGLA #1 en lecturas (§6.2): un rol no-Admin sólo ve órdenes de SU sucursal;
        // el resultado para otra sucursal es null, igual que si no existiera.
        // Se mapea en memoria: los totales se calculan en C# (no traducibles a SQL).
        IQueryable<OrdenVenta> q = db.OrdenesVenta;
        if (!ctx.EsAdmin) q = q.Where(o => o.SucursalId == ctx.SucursalId);
        var o = await q
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

    private static CotizacionListadoDto MapearACotizacionListadoDto(Cotizacion c)
    {
        // Un solo cálculo de totales por fila (§6.2 — DRY: antes se invocaba CalcularTotales 3 veces).
        var t = CalcularTotales(c.Detalles);
        return new CotizacionListadoDto(c.Id, c.FolioInterno ?? string.Empty, c.FechaEmision, c.Cliente!.Nombre, c.Vendedor?.Nombre, c.Estado, c.Detalles.Count,
            t.Subtotal, t.Iva, t.Total);
    }

    private static OrdenVentaListadoDto MapearAOrdenVentaListadoDto(OrdenVenta o)
    {
        var t = CalcularTotalesOrden(o.Detalles);
        return new OrdenVentaListadoDto(o.Id, o.FolioInterno ?? string.Empty, o.FechaEmision, o.Cliente!.Nombre, o.Vendedor?.Nombre, o.Estado, o.Detalles.Count,
            t.Subtotal, t.Iva, t.Total,
            o.CotizacionId > 0 ? o.CotizacionId : null);
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class VentasService
{
    private const int PageSizeHistorial = 25;

    public async Task<HistorialResultadoDto> HistorialAsync(
        int sucursalId, DateTime desde, DateTime hasta,
        string? texto, EstadoFactura? estado, int pagina, CancellationToken ct = default)
    {
        // REGLA #1: solo Admin puede consultar otra sucursal; el resto ve la suya.
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        if (desde > hasta) (desde, hasta) = (hasta, desde);
        // El rango de fechas llega en hora local del usuario; se convierte a UTC
        // (FechaEmision se guarda en UTC) e incluye todo el día "hasta".
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var q = db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == suc
                && f.FechaEmision >= desdeUtc && f.FechaEmision < hastaUtc);

        if (estado is not null)
            q = q.Where(f => f.Estado == estado);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(f => f.FolioInterno.ToLower().Contains(t)
                || (f.Cliente != null && f.Cliente.Nombre.ToLower().Contains(t)));
        }

        var resumen = await q
            .GroupBy(_ => 1)
            .Select(g => new HistorialResumenDto(
                g.Count(),
                g.Sum(f => f.Subtotal), g.Sum(f => f.Iva), g.Sum(f => f.Total)))
            .FirstOrDefaultAsync(ct) ?? new HistorialResumenDto(0, 0m, 0m, 0m);

        var paginas = Math.Max(1, (int)Math.Ceiling(resumen.Ventas / (double)PageSizeHistorial));
        pagina = Math.Clamp(pagina, 1, paginas);

        var filas = await q
            .OrderByDescending(f => f.FechaEmision)
            .Skip((pagina - 1) * PageSizeHistorial)
            .Take(PageSizeHistorial)
            .Select(f => new
            {
                f.Id, f.FolioInterno, f.FechaEmision, f.Estado,
                ClienteNombre = f.Cliente != null ? f.Cliente.Nombre : null,
                f.Subtotal, f.Iva, f.Total, f.UuidSat,
                Renglones = f.Detalles.Count
            })
            .ToListAsync(ct);

        // El enum viaja tipado al DTO; la conversión a texto (para UI) ocurre al renderizar.
        var ventas = filas.Select(f => new HistorialVentaDto(
            f.Id, f.FolioInterno, f.FechaEmision,
            f.ClienteNombre ?? "(sin cliente)",
            f.Estado,
            f.Subtotal, f.Iva, f.Total, f.UuidSat, f.Renglones)).ToList();

        return new HistorialResultadoDto(ventas, resumen, resumen.Ventas, pagina, paginas);
    }

    public async Task<HistorialDetalleDto?> HistorialDetalleAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cliente)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct);
        if (f is null) return null;
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId) return null; // aislamiento

        return new HistorialDetalleDto(
            f.Id, f.FolioInterno, f.FechaEmision,
            f.Cliente?.Nombre ?? "(sin cliente)", f.Estado,
            f.FormaPagoSat, f.MetodoPagoSat, f.UsoCfdi,
            f.Subtotal, f.Iva, f.Total, f.UuidSat,
            f.SelloDigital, f.CadenaOriginal,
            f.Detalles.Select(d => new HistorialLineaDto(
                d.Producto?.Nombre ?? $"Prod #{d.ProductoId}",
                d.Cantidad, d.UnidadNombre, d.PrecioUnitario, d.Importe)).ToList());
    }
}
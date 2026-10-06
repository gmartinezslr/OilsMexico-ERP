using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Notas de Crédito CFDI 4.0 (TipoComprobante=E, Uso G02, relación 01).
/// La devolución del Historial sigue siendo operativa (repone stock);
/// la NC es el documento fiscal electrónico con UUID propio.
/// </summary>
public sealed partial class NotaCreditoService(
    ErpDbContext db, ISucursalContext ctx,
    IPacTimbradoService pac, IConfiguration cfg) : INotaCreditoService
{
    public async Task<NotaCreditoPreviewDto> PreviewAsync(int facturaOrigenId, CancellationToken ct = default)
    {
        var f = await db.Facturas.AsNoTracking()
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cliente)
            .FirstOrDefaultAsync(x => x.Id == facturaOrigenId, ct)
            ?? throw new InvalidOperationException("Factura origen no existe.");
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes acreditar facturas de otra sucursal.");
        var previa = await db.NotasCredito.AsNoTracking()
            .AnyAsync(n => n.FacturaOrigenId == facturaOrigenId && n.Estado != "Cancelada", ct);
        var lineas = f.Detalles.Select(d => new NotaCreditoLineaDto(
            d.ProductoId,
            d.Producto != null ? $"{d.Producto.Sku} - {d.Producto.Nombre}" : $"Prod #{d.ProductoId}",
            d.Producto?.Sku ?? "",
            d.UnidadNombre, d.Cantidad, d.PrecioUnitario,
            Math.Round(d.Cantidad * d.PrecioUnitario, 2), d.LoteId)).ToList();
        return new NotaCreditoPreviewDto(
            f.Id, f.FolioInterno, f.Cliente?.Nombre ?? "(sin cliente)", f.UuidSat,
            f.Total, f.Estado.ToString(), previa, lineas,
            f.Subtotal, f.Iva, f.Total);
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public interface ITicketService
{
    Task<TicketVentaDto?> ObtenerTicketAsync(int facturaId, CancellationToken ct = default);
}

public sealed class TicketService(ErpDbContext db) : ITicketService
{
    public async Task<TicketVentaDto?> ObtenerTicketAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cliente).Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct);
        if (f is null) return null;
        var cajero = await db.Usuarios
            .Where(u => u.SucursalId == f.SucursalId && u.Activo)
            .Select(u => u.Nombre).FirstOrDefaultAsync(ct) ?? "Caja";
        return new TicketVentaDto(
            f.FolioInterno, f.FechaEmision, f.Sucursal?.Nombre ?? "",
            cajero, f.Cliente?.Nombre ?? "", f.FormaPagoSat,
            f.Detalles.Select(d => new TicketLineaDto(
                d.Producto?.Nombre ?? $"Prod #{d.ProductoId}",
                d.Cantidad, d.UnidadNombre, d.PrecioUnitario, d.Importe)).ToList(),
            f.Subtotal, f.Iva, f.Total, f.UuidSat);
    }
}

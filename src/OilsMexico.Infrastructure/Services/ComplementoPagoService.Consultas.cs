using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class ComplementoPagoService
{
    public async Task<List<RepListadoDto>> ListarAsync(
        int sucursalId, EstadoRep? estado, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var q = db.ComplementosPago.AsNoTracking().Where(r => r.SucursalId == suc);
        if (estado.HasValue) q = q.Where(r => r.Estado == estado.Value);
        return await q.OrderByDescending(r => r.FechaEmision).Take(200)
            .Select(r => new RepListadoDto(
                r.Id, r.FolioInterno, r.FechaEmision,
                r.Cliente != null ? r.Cliente.Nombre : "(sin cliente)",
                r.Total, r.UuidSat, r.Estado,
                r.Documentos.Count, r.XmlSellado != null))
            .ToListAsync(ct);
    }

    public async Task<RepDetalleDto?> DetalleAsync(int repId, CancellationToken ct = default)
    {
        var r = await db.ComplementosPago.AsNoTracking()
            .Include(x => x.Cliente).Include(x => x.Documentos)
            .FirstOrDefaultAsync(x => x.Id == repId, ct);
        if (r is null) return null;
        if (!ctx.EsAdmin && r.SucursalId != ctx.SucursalId) return null;
        return new RepDetalleDto(
            r.Id, r.FolioInterno, r.FechaEmision,
            r.Cliente?.Nombre ?? "(sin cliente)", r.Total, r.UuidSat, r.Estado,
            r.Documentos.Select(d => new RepDoctoDto(
                d.FolioFactura, d.UuidFactura, d.NumParcialidad,
                d.ImpSaldoAnt, d.ImpPagado, d.ImpSaldoInsoluto, d.Moneda)).ToList());
    }

    public async Task<string?> ObtenerXmlAsync(int repId, CancellationToken ct = default)
    {
        var r = await db.ComplementosPago.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == repId, ct);
        if (r is null) return null;
        if (!ctx.EsAdmin && r.SucursalId != ctx.SucursalId) return null;
        return r.XmlSellado;
    }
}

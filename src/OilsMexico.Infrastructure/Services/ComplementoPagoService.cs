using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Complemento de pagos / REP (TipoComprobante=P, Uso CP01).
/// Las ventas PPD nacen como cuentas por cobrar; cada cobro se ampara
/// en un REP con DoctoRelacionado (saldo anterior / pagado / insoluto).
/// 1 cobro = 1 REP (trazabilidad simple, sin consolidar receptores distintos).
/// </summary>
public sealed partial class ComplementoPagoService(
    ErpDbContext db, ISucursalContext ctx, ICfdiSelladoService sellado,
    IPacTimbradoService pac) : IComplementoPagoService
{
    public async Task<List<VentaPpdPendienteDto>> PendientesAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var facturas = await db.Facturas.AsNoTracking()
            .Include(f => f.Cliente)
            .Where(f => f.SucursalId == suc && f.MetodoPagoSat == "PPD"
                && f.Estado != Domain.Enums.EstadoFactura.Cancelada
                && f.Estado != Domain.Enums.EstadoFactura.Devolucion)
            .OrderBy(f => f.FechaEmision).Take(200).ToListAsync(ct);
        if (facturas.Count == 0) return [];
        var ids = facturas.Select(f => f.Id).ToList();
        var cobros = await db.VentaCobros.AsNoTracking()
            .Where(c => ids.Contains(c.FacturaId))
            .GroupBy(c => c.FacturaId)
            .Select(g => new { FacturaId = g.Key, Cobrado = g.Sum(c => c.Monto), N = g.Count() })
            .ToDictionaryAsync(x => x.FacturaId, ct);
        var res = new List<VentaPpdPendienteDto>();
        foreach (var f in facturas)
        {
            cobros.TryGetValue(f.Id, out var c);
            var cobrado = c?.Cobrado ?? 0m;
            var saldo = Math.Round(f.Total - cobrado, 2);
            if (saldo <= 0.01m) continue; // liquidada
            res.Add(new VentaPpdPendienteDto(
                f.Id, f.FolioInterno, f.FechaEmision,
                f.Cliente?.Nombre ?? "(sin cliente)", f.ClienteId,
                f.Total, cobrado, saldo, c?.N ?? 0));
        }
        return res;
    }
}

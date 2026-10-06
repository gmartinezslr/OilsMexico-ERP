using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class NotaCreditoService
{
    public async Task<List<NotaCreditoListadoDto>> ListarAsync(
        int sucursalId, string? estado, string? texto, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var q = db.NotasCredito.AsNoTracking().Where(n => n.SucursalId == suc);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(n => n.Estado == estado);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(n => n.FolioInterno.ToLower().Contains(t)
                || (n.Cliente != null && n.Cliente.Nombre.ToLower().Contains(t)));
        }
        return await q.OrderByDescending(n => n.FechaEmision).Take(200)
            .Select(n => new NotaCreditoListadoDto(
                n.Id, n.FolioInterno, n.FechaEmision,
                n.Cliente != null ? n.Cliente.Nombre : "(sin cliente)",
                n.FacturaOrigen != null ? n.FacturaOrigen.FolioInterno : $"#{n.FacturaOrigenId}",
                n.UuidSat, n.Motivo, n.Total, n.Estado, n.XmlSellado != null))
            .ToListAsync(ct);
    }

    public async Task<string?> ObtenerXmlAsync(int notaId, CancellationToken ct = default)
    {
        var n = await db.NotasCredito.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notaId, ct);
        if (n is null) return null;
        if (ctx.Rol != "Admin" && n.SucursalId != ctx.SucursalId) return null;
        return n.XmlSellado;
    }

    public async Task<NotaCreditoResult> CancelarAsync(int notaId, string motivo, CancellationToken ct = default)
    {
        var n = await db.NotasCredito.FirstOrDefaultAsync(x => x.Id == notaId, ct)
            ?? throw new InvalidOperationException("Nota de crédito no existe.");
        if (ctx.Rol is not ("Admin" or "Conta") && n.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cancelar NC de otra sucursal.");
        if (ctx.Rol is not ("Admin" or "Conta"))
            throw new UnauthorizedAccessException("Solo Admin/Conta cancelan NC.");
        if (n.Estado != "Timbrada")
            throw new InvalidOperationException($"Solo se cancelan NC timbradas (actual: {n.Estado}).");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("El motivo de cancelación es obligatorio (SAT).");
        n.Estado = "Cancelada";
        n.MotivoCancelacion = motivo.Trim();
        await db.SaveChangesAsync(ct);
        return new NotaCreditoResult(n.Id, n.FolioInterno, n.UuidSat,
            n.Subtotal, n.Iva, n.Total, "CANCELADA");
    }
}

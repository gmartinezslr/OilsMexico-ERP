using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CorteCajaService
{
    public async Task<CortePreviewDto> PreviewAsync(int corteId, CancellationToken ct = default)
    {
        var corte = await db.CortesCaja.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == corteId, ct)
            ?? throw new InvalidOperationException("Corte no existe.");
        if (ctx.Rol != "Admin" && corte.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes ver cortes de otra sucursal.");
        var ventas = await VentasTurnoAsync(corte.SucursalId, corte.FechaAperturaUtc, ct);
        return Clasificar(corte.Id, corte.FondoInicial, ventas);
    }

    public async Task<CorteResult> CerrarAsync(CerrarCorteRequest req, CancellationToken ct = default)
    {
        if (req.EfectivoContado < 0) throw new InvalidOperationException("El conteo no puede ser negativo.");
        var corte = await db.CortesCaja.FirstOrDefaultAsync(c => c.Id == req.CorteId, ct)
            ?? throw new InvalidOperationException("Corte no existe.");
        if (ctx.Rol != "Admin" && corte.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cerrar cortes de otra sucursal.");
        if (corte.Estado != "Abierto") throw new InvalidOperationException("El corte ya está cerrado.");
        var ventas = await VentasTurnoAsync(corte.SucursalId, corte.FechaAperturaUtc, ct);
        var foto = Clasificar(corte.Id, corte.FondoInicial, ventas);
        corte.TotalEfectivoSistema = foto.EfectivoSistema;
        corte.TotalTarjetaSistema = foto.TarjetaSistema;
        corte.TotalTransferSistema = foto.TransferSistema;
        corte.TotalOtrosSistema = foto.OtrosSistema;
        corte.TotalVentasSistema = foto.TotalVentasSistema;
        corte.NumVentas = foto.NumVentas;
        corte.EfectivoContado = Math.Round(req.EfectivoContado, 2);
        corte.Diferencia = Math.Round(corte.EfectivoContado - (corte.FondoInicial + corte.TotalEfectivoSistema), 2);
        corte.FechaCierreUtc = DateTime.UtcNow;
        corte.UsuarioCierreId = ctx.UsuarioId;
        corte.Estado = "Cerrado";
        corte.Observaciones = req.Observaciones?.Trim();
        await db.SaveChangesAsync(ct);
        return new CorteResult(corte.Id, corte.FechaCierreUtc, corte.TotalVentasSistema,
            corte.EfectivoContado, corte.Diferencia, "CERRADO");
    }

    public async Task<List<CorteListadoDto>> HistorialAsync(int sucursalId, int dias, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var desde = DateTime.UtcNow.AddDays(-Math.Clamp(dias, 1, 90));
        return await db.CortesCaja.AsNoTracking()
            .Where(c => c.SucursalId == suc && c.FechaAperturaUtc >= desde)
            .OrderByDescending(c => c.FechaAperturaUtc).Take(100)
            .Select(c => new CorteListadoDto(
                c.Id, c.FechaAperturaUtc, c.FechaCierreUtc, c.UsuarioAperturaNombre,
                c.FondoInicial, c.TotalVentasSistema, c.EfectivoContado,
                c.Diferencia, c.Estado, c.NumVentas))
            .ToListAsync(ct);
    }

    private async Task<List<(string Forma, decimal Total)>> VentasTurnoAsync(
        int sucursalId, DateTime aperturaUtc, CancellationToken ct)
        => await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId
                && f.FechaEmision >= aperturaUtc
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion)
            .Select(f => new ValueTuple<string, decimal>(f.FormaPagoSat, f.Total))
            .ToListAsync(ct);

    private static CortePreviewDto Clasificar(
        int corteId, decimal fondo, IEnumerable<(string Forma, decimal Total)> ventas)
    {
        decimal ef = 0, tj = 0, tr = 0, ot = 0;
        int n = 0;
        foreach (var (forma, total) in ventas)
        {
            n++;
            switch (forma)
            {
                case "01": ef += total; break;
                case "04": case "28": tj += total; break;
                case "03": tr += total; break;
                default: ot += total; break;
            }
        }
        return new CortePreviewDto(corteId, n, fondo,
            Math.Round(ef, 2), Math.Round(tj, 2), Math.Round(tr, 2), Math.Round(ot, 2),
            Math.Round(ef + tj + tr + ot, 2));
    }
}

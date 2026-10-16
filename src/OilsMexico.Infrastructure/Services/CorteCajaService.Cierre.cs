using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CorteCajaService
{
    public async Task<CortePreviewDto> PreviewAsync(int corteId, CancellationToken ct = default)
    {
        var corte = await db.CortesCaja.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == corteId, ct)
            ?? throw new InvalidOperationException("Corte no existe.");
        if (!ctx.EsAdmin && corte.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes ver cortes de otra sucursal.");
        var ventas = await VentasTurnoAsync(corte.SucursalId, corte.FechaAperturaUtc, ct);
        var gastos = await GastosEfectivoTurnoAsync(corte.SucursalId, corte.FechaAperturaUtc, ct);
        var foto = Clasificar(corte.Id, corte.FondoInicial, ventas);
        foto.GastosEfectivo = Math.Round(gastos, 2);
        return foto;
    }

    public async Task<CorteResult> CerrarAsync(CerrarCorteRequest req, CancellationToken ct = default)
    {
        ExigirActorAuditoria();
        if (req.EfectivoContado < 0) throw new InvalidOperationException("El conteo no puede ser negativo.");
        var corte = await db.CortesCaja.FirstOrDefaultAsync(c => c.Id == req.CorteId, ct)
            ?? throw new InvalidOperationException("Corte no existe.");
        if (!ctx.EsAdmin && corte.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cerrar cortes de otra sucursal.");
        if (corte.Estado != EstadoCorte.Abierto) throw new InvalidOperationException("El corte ya está cerrado.");
        var ventas = await VentasTurnoAsync(corte.SucursalId, corte.FechaAperturaUtc, ct);
        var foto = Clasificar(corte.Id, corte.FondoInicial, ventas);
        foto.GastosEfectivo = Math.Round(
            await GastosEfectivoTurnoAsync(corte.SucursalId, corte.FechaAperturaUtc, ct), 2);
        corte.TotalEfectivoSistema = foto.EfectivoSistema;
        corte.TotalTarjetaSistema = foto.TarjetaSistema;
        corte.TotalTransferSistema = foto.TransferSistema;
        corte.TotalOtrosSistema = foto.OtrosSistema;
        corte.TotalVentasSistema = foto.TotalVentasSistema;
        corte.NumVentas = foto.NumVentas;
        corte.EfectivoContado = Math.Round(req.EfectivoContado, 2);
        // El efectivo esperado descuenta los gastos en efectivo del turno (egresos de caja).
        corte.Diferencia = Math.Round(corte.EfectivoContado - foto.EfectivoNeto, 2);
        corte.FechaCierreUtc = DateTime.UtcNow;
        corte.UsuarioCierreId = ctx.UsuarioId;
        corte.Estado = EstadoCorte.Cerrado;
        corte.Observaciones = req.Observaciones?.Trim();
        RegistrarAuditoria("CajaCerrada", $"Corte #{corte.Id} cerrado en sucursal {corte.SucursalId}.",
            $"Estado=Abierto; FondoInicial={corte.FondoInicial:N2}",
            $"Estado=Cerrado; EfectivoContado={corte.EfectivoContado:N2}; " +
            $"TotalVentas={corte.TotalVentasSistema:N2}; Diferencia={corte.Diferencia:N2}",
            corte.FechaCierreUtc.Value);
        await db.SaveChangesAsync(ct);
        return new CorteResult(corte.Id, corte.FechaCierreUtc, corte.TotalVentasSistema,
            corte.EfectivoContado, corte.Diferencia, EstadoCorte.Cerrado);
    }

    public async Task<List<CorteListadoDto>> HistorialAsync(int sucursalId, int dias, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
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

    /// <summary>Egresos en efectivo del turno: gastos operativos con forma 01 desde la apertura.</summary>
    private async Task<decimal> GastosEfectivoTurnoAsync(
        int sucursalId, DateTime aperturaUtc, CancellationToken ct)
        => await db.Gastos.AsNoTracking()
            .Where(g => g.SucursalId == sucursalId
                && g.Fecha >= aperturaUtc
                && g.FormaPagoSat == "01")
            .SumAsync(g => g.Importe, ct);

    private CortePreviewDto Clasificar(
        int corteId, decimal fondo, IEnumerable<(string Forma, decimal Total)> ventas)
    {
        // T-13: Clasificación de formas de pago basada en el campo 'grupo' de catalogos_sat (OCP).
        // Evita el switch estático y permite incorporar nuevos métodos de pago sin alterar código.
        var grupos = sat?.Obtener().GruposFormasPago;
        decimal ef = 0, tj = 0, tr = 0, ot = 0;
        int n = 0;
        foreach (var (forma, total) in ventas)
        {
            n++;
            string? grupo = null;
            if (grupos != null && grupos.TryGetValue(forma, out var g))
                grupo = g?.ToLowerInvariant();
            else if (sat != null)
                grupo = sat.ObtenerGrupoFormaPago(forma);
            else
                grupo = forma switch
                {
                    "01" => "efectivo",
                    "04" or "28" => "tarjeta",
                    "03" => "transferencia",
                    _ => "otro"
                };

            switch (grupo)
            {
                case "efectivo": ef += total; break;
                case "tarjeta": tj += total; break;
                case "transferencia": tr += total; break;
                default: ot += total; break;
            }
        }
        return new CortePreviewDto(corteId, n, fondo,
            Math.Round(ef, 2), Math.Round(tj, 2), Math.Round(tr, 2), Math.Round(ot, 2),
            Math.Round(ef + tj + tr + ot, 2));
    }
}

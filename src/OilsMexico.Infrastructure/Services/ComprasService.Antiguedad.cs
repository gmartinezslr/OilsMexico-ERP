using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// §1.2 — Antigüedad de saldos CxP con intereses moratorios configurables.
/// Fuente: Compras recibidas/parciales con saldo pendiente (Total - MontoPagado).
/// FechaVencimiento comprometida por compra; datos históricos se backfillean con los días del proveedor.
/// Tasa moratoria: clave <c>cxp_tasa_moratoria_diaria</c> en tabla configuracion.
/// REGLA #1: sucursal resuelta con ResolverSucursalAutorizada.
/// </summary>
public sealed partial class ComprasService
{
    // Clave de configuración para la tasa moratoria diaria CxP.
    public const string ClaveTasaMoratoriaCxp = "cxp_tasa_moratoria_diaria";

    public async Task<AntiguedadSaldosCxpDto> AntiguedadSaldosCxpAsync(
        int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);

        var tasaDiaria = await ObtenerTasaCxpAsync(ct);

        var hoy = DateTime.UtcNow.Date;

        // Compras recibidas/parciales con saldo pendiente de pago.
        var compras = await db.Compras.AsNoTracking()
            .Where(c => c.SucursalId == suc
                && (c.Estado == EstadoCompra.Recibida || c.Estado == EstadoCompra.Parcial)
                && c.EstadoPago != EstadoPagoCompra.Pagada
                && c.MontoPagado < c.Total)
            .Select(c => new
            {
                c.Id, c.FolioInterno, c.FechaEmision, c.FechaVencimiento,
                c.Total, c.MontoPagado, c.ProveedorId,
                ProveedorNombre = c.Proveedor == null ? "Sin proveedor" : c.Proveedor.Nombre
            })
            .ToListAsync(ct);

        if (compras.Count == 0)
            return new AntiguedadSaldosCxpDto(0, 0, 0, 0, 0, 0, []);

        // Agrupar por proveedor.
        var porProveedor = compras.GroupBy(c => c.ProveedorId);
        var resultados = new List<AntiguedadProveedorDto>();

        foreach (var grupo in porProveedor)
        {
            var comprasVencidas = new List<CompraVencidaDto>();
            decimal r0 = 0, r16 = 0, r31 = 0, r60 = 0, totalInteres = 0;
            var nombreProv = grupo.First().ProveedorNombre;

            foreach (var c in grupo)
            {
                var saldo = Math.Round(c.Total - c.MontoPagado, 2);
                if (saldo <= 0.01m) continue;

                var fechaVto = (c.FechaVencimiento ?? c.FechaEmision.AddDays(30)).Date;
                var diasVencida = Math.Max(0, (hoy - fechaVto).Days);

                var interes = tasaDiaria > 0 && diasVencida > 0
                    ? Math.Round(saldo * tasaDiaria * diasVencida, 2)
                    : 0m;

                totalInteres += interes;
                comprasVencidas.Add(new CompraVencidaDto(
                    c.Id, c.FolioInterno, c.FechaEmision,
                    DateTime.SpecifyKind(fechaVto, DateTimeKind.Utc),
                    nombreProv, diasVencida, saldo, interes));

                if (diasVencida <= 15) r0 += saldo;
                else if (diasVencida <= 30) r16 += saldo;
                else if (diasVencida <= 60) r31 += saldo;
                else r60 += saldo;
            }

            if (comprasVencidas.Count == 0) continue;

            resultados.Add(new AntiguedadProveedorDto(
                grupo.Key, nombreProv,
                Math.Round(r0, 2), Math.Round(r16, 2),
                Math.Round(r31, 2), Math.Round(r60, 2),
                Math.Round(r0 + r16 + r31 + r60, 2),
                Math.Round(totalInteres, 2),
                comprasVencidas.OrderByDescending(c => c.DiasVencida).ToList()));
        }

        resultados = resultados.OrderByDescending(p => p.TotalSaldo).ToList();

        return new AntiguedadSaldosCxpDto(
            Math.Round(resultados.Sum(p => p.Rango0_15), 2),
            Math.Round(resultados.Sum(p => p.Rango16_30), 2),
            Math.Round(resultados.Sum(p => p.Rango31_60), 2),
            Math.Round(resultados.Sum(p => p.Rango60Mas), 2),
            Math.Round(resultados.Sum(p => p.TotalSaldo), 2),
            Math.Round(resultados.Sum(p => p.TotalInteres), 2),
            resultados);
    }

    private async Task<decimal> ObtenerTasaCxpAsync(CancellationToken ct)
    {
        var raw = await db.Configuracion.AsNoTracking()
            .Where(c => c.Clave == ClaveTasaMoratoriaCxp).Select(c => c.Valor)
            .FirstOrDefaultAsync(ct);
        var sep = System.Globalization.CultureInfo.InvariantCulture;
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number, sep, out var v) && v >= 0
            ? v : 0m;
    }
}

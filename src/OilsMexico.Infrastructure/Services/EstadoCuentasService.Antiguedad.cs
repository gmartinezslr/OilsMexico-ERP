using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// §1.2 — Antigüedad de saldos CxC con intereses moratorios configurables.
/// Fuente: Facturas abiertas + VentaCobros (mismo criterio cash-basis que el MVP).
/// FechaVencimiento = FechaEmision + Cliente.DiasCredito (0 = contado = ya venció al emitir).
/// Tasa moratoria: clave <c>cxc_tasa_moratoria_diaria</c> en tabla configuracion (decimal, default 0).
/// REGLA #1: sucursal resuelta con ResolverSucursalAutorizada.
/// </summary>
public sealed partial class EstadoCuentasService
{
    // Clave de configuración para la tasa moratoria diaria CxC.
    public const string ClaveTasaMoratoriaCxc = "cxc_tasa_moratoria_diaria";

    public async Task<AntiguedadSaldosCxcDto> AntiguedadSaldosAsync(
        int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);

        // Tasa moratoria diaria (0.0005 = 0.05% diario ≈ 18% anual). Default 0 = sin intereses.
        var tasaDiaria = await ObtenerTasaDiariaAsync(ClaveTasaMoratoriaCxc, ct);

        var hoy = DateTime.UtcNow.Date;

        // Facturas abiertas de la sucursal (excluye canceladas y devoluciones).
        var facturas = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == suc
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion)
            .Select(f => new
            {
                f.Id, f.FolioInterno, f.FechaEmision, f.Total, f.ClienteId,
                f.MetodoPagoSat, DiasCredito = f.Cliente == null ? 0 : f.Cliente.DiasCredito
            })
            .ToListAsync(ct);

        if (facturas.Count == 0)
            return new AntiguedadSaldosCxcDto(0, 0, 0, 0, 0, 0, []);

        var clienteIds = facturas.Select(f => f.ClienteId).Distinct().ToList();
        var facturaIds = facturas.Select(f => f.Id).ToList();

        // Cobros aplicados por factura (solo de facturas vigentes).
        var cobrosPorFactura = await db.VentaCobros.AsNoTracking()
            .Where(v => v.FacturaId != null && facturaIds.Contains(v.FacturaId)
                && (v.Factura == null
                    || (v.Factura.Estado != EstadoFactura.Cancelada
                        && v.Factura.Estado != EstadoFactura.Devolucion)))
            .GroupBy(v => v.FacturaId)
            .Select(g => new { FacturaId = g.Key, Cobrado = g.Sum(v => v.Monto) })
            .ToDictionaryAsync(x => x.FacturaId, x => x.Cobrado, ct);

        // Clientes con días de crédito.
        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => clienteIds.Contains(c.Id) && c.Activo)
            .Select(c => new { c.Id, c.Nombre, c.Rfc, c.Telefono, c.Email })
            .ToListAsync(ct);

        var clienteMap = clientes.ToDictionary(c => c.Id);

        // Agrupar por cliente.
        var porCliente = facturas
            .Where(f => clienteMap.ContainsKey(f.ClienteId))
            .GroupBy(f => f.ClienteId);

        var resultadosCliente = new List<AntiguedadClienteDto>();

        foreach (var grupo in porCliente)
        {
            var cl = clienteMap[grupo.Key];
            var facturasVencidas = new List<FacturaVencidaDto>();

            decimal r0 = 0, r16 = 0, r31 = 0, r60 = 0, totalInteres = 0;

            foreach (var f in grupo)
            {
                var cobrado = cobrosPorFactura.GetValueOrDefault(f.Id, 0m);
                var saldo = Math.Round(f.Total - cobrado, 2);
                if (saldo <= 0.01m) continue;   // ya pagada completamente

                // Crédito acordado en el cliente; PUE se liquida al emitir aunque el cliente tenga plazo.
                var diasCredito = f.MetodoPagoSat == "PUE" ? 0 : Math.Max(0, f.DiasCredito);
                var fechaVto = f.FechaEmision.AddDays(diasCredito).Date;
                var diasVencida = Math.Max(0, (hoy - fechaVto).Days);

                var interes = tasaDiaria > 0 && diasVencida > 0
                    ? Math.Round(saldo * tasaDiaria * diasVencida, 2)
                    : 0m;

                totalInteres += interes;
                facturasVencidas.Add(new FacturaVencidaDto(
                    f.Id, f.FolioInterno, f.FechaEmision,
                    DateTime.SpecifyKind(fechaVto, DateTimeKind.Utc),
                    diasVencida, saldo, interes));

                // Acumular rangos
                if (diasVencida <= 15) r0 += saldo;
                else if (diasVencida <= 30) r16 += saldo;
                else if (diasVencida <= 60) r31 += saldo;
                else r60 += saldo;
            }

            if (facturasVencidas.Count == 0) continue;

            resultadosCliente.Add(new AntiguedadClienteDto(
                cl.Id, cl.Nombre, cl.Rfc, cl.Telefono ?? "", cl.Email ?? "",
                Math.Round(r0, 2), Math.Round(r16, 2),
                Math.Round(r31, 2), Math.Round(r60, 2),
                Math.Round(r0 + r16 + r31 + r60, 2),
                Math.Round(totalInteres, 2),
                facturasVencidas.OrderByDescending(f => f.DiasVencida).ToList()));
        }

        resultadosCliente = resultadosCliente.OrderByDescending(c => c.TotalSaldo).ToList();

        return new AntiguedadSaldosCxcDto(
            Math.Round(resultadosCliente.Sum(c => c.Rango0_15), 2),
            Math.Round(resultadosCliente.Sum(c => c.Rango16_30), 2),
            Math.Round(resultadosCliente.Sum(c => c.Rango31_60), 2),
            Math.Round(resultadosCliente.Sum(c => c.Rango60Mas), 2),
            Math.Round(resultadosCliente.Sum(c => c.TotalSaldo), 2),
            Math.Round(resultadosCliente.Sum(c => c.TotalInteres), 2),
            resultadosCliente);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<decimal> ObtenerTasaDiariaAsync(string clave, CancellationToken ct)
    {
        var raw = await db.Configuracion.AsNoTracking()
            .Where(c => c.Clave == clave).Select(c => c.Valor).FirstOrDefaultAsync(ct);
        var sep = System.Globalization.CultureInfo.InvariantCulture;
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number, sep, out var v) && v >= 0
            ? v : 0m;
    }
}

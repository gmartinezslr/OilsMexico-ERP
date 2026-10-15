using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Reportes financieros §1.4 (parte 1: balanza + balance).</summary>
public sealed partial class ReportesFinancierosService(
    ErpDbContext db, ISucursalContext ctx, IAsientoGeneradorService generador) : IReportesFinancierosService
{
    public async Task<BalanzaComprobacionDto> BalanzaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var (ini, fin) = Rango(desde, hasta);
        var cuentas = await db.CuentasContables.AsNoTracking()
            .Where(c => c.SucursalId == suc).OrderBy(c => c.Codigo).ToListAsync(ct);
        var movs = await db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable!.SucursalId == suc
                && d.AsientoContable.FechaUtc >= ini && d.AsientoContable.FechaUtc < fin)
            .GroupBy(d => d.CuentaId)
            .Select(g => new { CuentaId = g.Key,
                Deb = g.Where(d => d.TipoMovimiento == MovimientoTipo.Debito).Sum(d => d.Importe),
                Cre = g.Where(d => d.TipoMovimiento == MovimientoTipo.Credito).Sum(d => d.Importe) })
            .ToDictionaryAsync(x => x.CuentaId, ct);
        var ren = new List<BalanzaRenglonDto>();
        foreach (var c in cuentas)
        {
            movs.TryGetValue(c.Id, out var m);
            var deb = m?.Deb ?? 0m; var cre = m?.Cre ?? 0m;
            var saldo = c.Tipo is 1 or 5 ? deb - cre : cre - deb;
            ren.Add(new BalanzaRenglonDto(c.Id, c.Codigo, c.Nombre, c.Tipo,
                Math.Round(deb, 2), Math.Round(cre, 2),
                Math.Round(saldo >= 0 ? saldo : 0m, 2), Math.Round(saldo < 0 ? -saldo : 0m, 2)));
        }
        return new BalanzaComprobacionDto(suc, desde.Date, hasta.Date, ren,
            Math.Round(ren.Sum(r => r.Debitos), 2), Math.Round(ren.Sum(r => r.Creditos), 2),
            Math.Round(ren.Sum(r => r.SaldoDeudor), 2), Math.Round(ren.Sum(r => r.SaldoAcreedor), 2));
    }

    public async Task<BalanceGeneralDto> BalanceAsync(int sucursalId, DateTime corte, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var corteUtc = DateTime.SpecifyKind(corte.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var cuentas = await db.CuentasContables.AsNoTracking()
            .Where(c => c.SucursalId == suc).OrderBy(c => c.Codigo).ToListAsync(ct);
        var post = await db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable!.SucursalId == suc && d.AsientoContable.FechaUtc >= corteUtc)
            .GroupBy(d => d.CuentaId)
            .Select(g => new { CuentaId = g.Key,
                Deb = g.Where(d => d.TipoMovimiento == MovimientoTipo.Debito).Sum(d => d.Importe),
                Cre = g.Where(d => d.TipoMovimiento == MovimientoTipo.Credito).Sum(d => d.Importe) })
            .ToDictionaryAsync(x => x.CuentaId, ct);
        decimal AlCorte(int id, int tipo, decimal actual)
        {
            post.TryGetValue(id, out var m);
            var neto = (m?.Deb ?? 0m) - (m?.Cre ?? 0m);
            return tipo is 1 or 5 ? actual - neto : actual + neto;
        }
        // SaldoActual guarda signo deudor (débito +, crédito −) en todas las cuentas.
        // Para el Balance se presenta cada sección en su naturaleza: Activo/Gasto en
        // deudor, Pasivo/Patrimonio/Ingreso en acreedor.
        static decimal Normal(int tipo, decimal saldoDeudor) =>
            tipo is 1 or 5 ? saldoDeudor : -saldoDeudor;
        List<BalanceCuentaDto> Sec(int tipo) => cuentas.Where(c => c.Tipo == tipo)
            .Select(c => new BalanceCuentaDto(c.Codigo, c.Nombre, Math.Round(Normal(c.Tipo, AlCorte(c.Id, c.Tipo, c.SaldoActual)), 2))).ToList();
        var act = Sec(1); var pas = Sec(2); var pat = Sec(3);
        // Resultado del ejercicio (utilidad positiva): ingresos − gastos, en naturaleza
        // acreedora. Las cuentas de ingreso/gasto guardan SaldoActual con signo deudor
        // (débito suma, crédito resta), así que se invierte el signo para el Balance.
        var ing = -cuentas.Where(c => c.Tipo == 4).Sum(c => AlCorte(c.Id, 4, c.SaldoActual));
        // Los gastos son de naturaleza deudora: su SaldoActual ya es positivo.
        var gas = cuentas.Where(c => c.Tipo == 5).Sum(c => AlCorte(c.Id, 5, c.SaldoActual));
        var resultado = Math.Round(ing - gas, 2);
        var totalAct = Math.Round(act.Sum(c => c.Saldo), 2);
        var totalPas = Math.Round(pas.Sum(c => c.Saldo), 2);
        var totalPat = Math.Round(pat.Sum(c => c.Saldo) + resultado, 2);
        return new BalanceGeneralDto(suc, corte.Date,
            new BalanceSeccionDto("Activo", act, totalAct),
            new BalanceSeccionDto("Pasivo", pas, totalPas),
            new BalanceSeccionDto("Patrimonio", [.. pat, new BalanceCuentaDto("—", "Resultado del ejercicio", resultado)], totalPat),
            totalAct, Math.Round(totalPas + totalPat, 2),
            Math.Abs(totalAct - (totalPas + totalPat)) <= 0.01m);
    }

    private static (DateTime Ini, DateTime Fin) Rango(DateTime desde, DateTime hasta)
    {
        var ini = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var fin = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        return (ini, fin);
    }
}

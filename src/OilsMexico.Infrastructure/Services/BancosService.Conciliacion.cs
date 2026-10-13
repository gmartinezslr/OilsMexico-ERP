using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Conciliación (empatar extracto ↔ ERP) y resumen de saldos. Ver BancosService.</summary>
public sealed partial class BancosService
{
    public async Task ConciliarAsync(ConciliarMovimientoRequest request, CancellationToken ct = default)
    {
        var mov = await db.MovimientosBancarios
            .Include(m => m.CuentaBancaria)
            .FirstOrDefaultAsync(m => m.Id == request.MovimientoId, ct)
            ?? throw new InvalidOperationException("El movimiento bancario no existe.");
        if (mov.CuentaBancaria is not null && !ctx.EsAdmin && mov.CuentaBancaria.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes conciliar movimientos de otra sucursal.");
        if (mov.Estado == EstadoConciliacion.Conciliado)
            throw new InvalidOperationException("El movimiento ya está conciliado.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Regla de naturaleza: Abono (entró dinero) ↔ Cobro; Cargo (salió dinero) ↔ Pago.
        var origen = (request.Origen ?? string.Empty).Trim();
        if (string.Equals(origen, "Cobro", StringComparison.OrdinalIgnoreCase))
        {
            if (mov.Naturaleza != NaturalezaMovimiento.Abono)
                throw new InvalidOperationException("Un Abono (entrada de dinero) sólo se concilia contra un Cobro.");
            var cobro = await db.VentaCobros.FirstOrDefaultAsync(v => v.Id == request.MovimientoErpId, ct)
                ?? throw new InvalidOperationException("El cobro no existe.");
            if (await db.MovimientosBancarios.AnyAsync(m => m.VentaCobroId == cobro.Id && m.Id != mov.Id, ct))
                throw new InvalidOperationException("Ese cobro ya está conciliado en otro movimiento.");
            mov.VentaCobroId = cobro.Id;
        }
        else if (string.Equals(origen, "Pago", StringComparison.OrdinalIgnoreCase))
        {
            if (mov.Naturaleza != NaturalezaMovimiento.Cargo)
                throw new InvalidOperationException("Un Cargo (salida de dinero) sólo se concilia contra un Pago a proveedor.");
            var pago = await db.CompraPagos.FirstOrDefaultAsync(p => p.Id == request.MovimientoErpId, ct)
                ?? throw new InvalidOperationException("El pago no existe.");
            if (await db.MovimientosBancarios.AnyAsync(m => m.CompraPagoId == pago.Id && m.Id != mov.Id, ct))
                throw new InvalidOperationException("Ese pago ya está conciliado en otro movimiento.");
            mov.CompraPagoId = pago.Id;
        }
        else
        {
            throw new InvalidOperationException("Origen inválido: use «Cobro» o «Pago».");
        }

        mov.Estado = EstadoConciliacion.Conciliado;
        mov.ConciliadoUtc = DateTime.UtcNow;
        mov.ConciliadoPorUsuarioId = ctx.UsuarioId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DesconciliarAsync(int movimientoId, CancellationToken ct = default)
    {
        var mov = await db.MovimientosBancarios
            .Include(m => m.CuentaBancaria)
            .FirstOrDefaultAsync(m => m.Id == movimientoId, ct)
            ?? throw new InvalidOperationException("El movimiento bancario no existe.");
        if (mov.CuentaBancaria is not null && !ctx.EsAdmin && mov.CuentaBancaria.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes modificar movimientos de otra sucursal.");
        if (mov.Estado == EstadoConciliacion.Pendiente) return; // ya pendiente: idempotente.

        mov.Estado = EstadoConciliacion.Pendiente;
        mov.VentaCobroId = null;
        mov.CompraPagoId = null;
        mov.ConciliadoUtc = null;
        mov.ConciliadoPorUsuarioId = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ResumenConciliacionDto> ResumenAsync(int cuentaId, CancellationToken ct = default)
    {
        var cuenta = await db.CuentasBancarias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cuentaId, ct)
            ?? throw new InvalidOperationException("La cuenta bancaria no existe.");
        if (!ctx.EsAdmin && cuenta.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes ver el resumen de otra sucursal.");

        var agg = await db.MovimientosBancarios.AsNoTracking()
            .Where(m => m.CuentaBancariaId == cuentaId)
            .GroupBy(m => m.CuentaBancariaId)
            .Select(g => new
            {
                Abonos = g.Where(m => m.Naturaleza == NaturalezaMovimiento.Abono).Sum(m => m.Monto),
                Cargos = g.Where(m => m.Naturaleza == NaturalezaMovimiento.Cargo).Sum(m => m.Monto),
                Pendientes = g.Count(m => m.Estado == EstadoConciliacion.Pendiente),
                Conciliados = g.Count(m => m.Estado == EstadoConciliacion.Conciliado)
            })
            .FirstOrDefaultAsync(ct);

        var abonos = agg?.Abonos ?? 0m;
        var cargos = agg?.Cargos ?? 0m;
        return new ResumenConciliacionDto(
            cuenta.Id, cuenta.Banco, cuenta.Alias, cuenta.Moneda, cuenta.SaldoInicial,
            abonos, cargos, Math.Round(cuenta.SaldoInicial + abonos - cargos, 2),
            agg?.Pendientes ?? 0, agg?.Conciliados ?? 0);
    }
}

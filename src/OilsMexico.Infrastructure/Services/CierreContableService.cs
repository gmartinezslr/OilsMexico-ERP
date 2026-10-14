using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Cierre de periodo contable mensual (checklist §1.3).
/// Cerrar = generar la póliza "Costo de ventas FIFO vs. Almacén" fechada al cierre del mes y
/// marcar el periodo en <c>periodos_contables</c> (bandera que hace que RegistrarAsientoAsync
/// rechace escrituras en ese mes, incluso de Admin). Reabrir = solo Admin: elimina la póliza
/// de cierre, revierte sus saldos y la bandera.
/// </summary>
public sealed class CierreContableService(
    ErpDbContext db, ISucursalContext ctx,
    IAsientoGeneradorService generador, ICuentasContablesService cuentasSvc) : ICierreContableService
{
    public async Task<List<PeriodoContableDto>> ListarAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var cierres = await db.PeriodosContables.AsNoTracking()
            .Where(p => p.SucursalId == suc)
            .OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes)
            .ToListAsync(ct);
        var resultado = new List<PeriodoContableDto>(cierres.Count);
        foreach (var p in cierres)
            resultado.Add(new PeriodoContableDto(p.SucursalId, p.Anio, p.Mes, p.CerradoUtc,
                p.UsuarioCierreId, p.Notas, await CostoDelCierreAsync(suc, p.Anio, p.Mes, ct)));
        return resultado;
    }

    public async Task<bool> EstaCerradoAsync(int sucursalId, DateTime fecha, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var local = ALocal(fecha);
        return await db.PeriodosContables.AsNoTracking().AnyAsync(p =>
            p.SucursalId == suc && p.Anio == local.Year && p.Mes == local.Month, ct);
    }

    public async Task<CierreContableResultDto> CerrarAsync(int sucursalId, int anio, int mes, CancellationToken ct = default)
    {
        if (!ctx.Establecida || ctx.Rol is not (Roles.Admin or Roles.Conta))
            throw new UnauthorizedAccessException("Solo los roles Admin y Conta pueden cerrar periodos contables.");
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        ValidarPeriodo(anio, mes);
        var ahora = DateTime.Now;
        if (anio > ahora.Year || (anio == ahora.Year && mes > ahora.Month))
            throw new InvalidOperationException("No se puede cerrar un periodo futuro.");
        if (await EstaCerradoAsync(suc, new DateTime(anio, mes, 1), ct))
            throw new InvalidOperationException($"El periodo {anio:0000}-{mes:00} ya está cerrado.");

        // Idempotencia por concepto marcador + índice único (sucursal, año, mes) como guard de
        // concurrencia: un cierre duplicado revierte en PostgreSQL sin necesidad de transacción.
        await CuentasContablesService.AsegurarCatalogoAsync(db, suc, ct);
        var desde = new DateTime(anio, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var concepto = CierresContables.ConceptoCierre(anio, mes);
        var lineas = await generador.GenerarAsientosDeCostoVentaAsync(suc, desde, hasta, ct);
        int? asientoId = null;
        var costo = 0m;
        var mensaje = "Periodo cerrado sin póliza: no hubo costo de ventas en el periodo.";
        if (lineas.Count >= 2)
        {
            // Un cierre que quedó a la medias (asiento sin bandera) se reutiliza: no se duplica póliza.
            var previo = await db.AsientosContables.AsNoTracking().FirstOrDefaultAsync(a =>
                a.SucursalId == suc && a.Tipo == CierresContables.TipoAsiento && a.Concepto == concepto, ct);
            if (previo is not null)
            {
                asientoId = previo.Id;
                costo = await CostoDelCierreAsync(suc, anio, mes, ct);
                mensaje = $"Periodo cerrado; se reutilizó la póliza de cierre AS-{previo.Id}.";
            }
            else
            {
                var codigos = lineas.Select(l => l.CuentaCodigo).Distinct().ToList();
                var cuentas = await db.CuentasContables.AsNoTracking()
                    .Where(c => c.SucursalId == suc && codigos.Contains(c.Codigo))
                    .ToDictionaryAsync(c => c.Codigo, c => c.Id, ct);
                var faltantes = codigos.Where(c => !cuentas.ContainsKey(c)).ToList();
                if (faltantes.Count > 0)
                {
                    mensaje = $"Periodo cerrado SIN póliza: faltan las cuentas " +
                              $"{string.Join(", ", faltantes)} en el catálogo de la sucursal.";
                }
                else
                {
                    var detalle = lineas.Select(l => new RegistrarDetalleAsientoRequest(
                        cuentas[l.CuentaCodigo], l.Debe ? MovimientoTipo.Debito : MovimientoTipo.Credito,
                        l.Importe, concepto)).ToList();
                    // Último instante local del mes: la póliza cae dentro del periodo que cierra.
                    var fechaCierre = DateTime.SpecifyKind(desde.AddMonths(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();
                    var res = await cuentasSvc.RegistrarAsientoAsync(new RegistrarAsientoRequest(
                        suc, ctx.UsuarioId, CierresContables.TipoAsiento, concepto, detalle, fechaCierre), ct);
                    asientoId = res.AsientoId;
                    costo = res.TotalDebitos;
                    mensaje = $"Periodo cerrado. Póliza de costo de ventas AS-{res.AsientoId} por {res.TotalDebitos:N2}.";
                }
            }
        }

        db.PeriodosContables.Add(new PeriodoContable
        {
            SucursalId = suc, Anio = anio, Mes = mes, UsuarioCierreId = ctx.UsuarioId,
            CerradoUtc = DateTime.UtcNow, Notas = asientoId.HasValue ? $"AS-{asientoId}" : "sin póliza"
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (await db.PeriodosContables.AsNoTracking().AnyAsync(
                    p => p.SucursalId == suc && p.Anio == anio && p.Mes == mes, ct))
                throw new InvalidOperationException(
                    $"El periodo {anio:0000}-{mes:00} ya fue cerrado por otro proceso.", ex);
            throw;
        }
        return new CierreContableResultDto(suc, anio, mes, asientoId, Math.Round(costo, 2), true, mensaje);
    }
    public async Task ReabrirAsync(int sucursalId, int anio, int mes, CancellationToken ct = default)
    {
        if (!ctx.EsAdmin)
            throw new UnauthorizedAccessException("Solo un administrador puede reabrir periodos contables.");
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        ValidarPeriodo(anio, mes);
        var flag = await db.PeriodosContables.FirstOrDefaultAsync(
            p => p.SucursalId == suc && p.Anio == anio && p.Mes == mes, ct)
            ?? throw new InvalidOperationException($"El periodo {anio:0000}-{mes:00} no está cerrado.");

        var concepto = CierresContables.ConceptoCierre(anio, mes);
        var asiento = await db.AsientosContables.Include(a => a.Detalles)
            .FirstOrDefaultAsync(a => a.SucursalId == suc && a.Tipo == CierresContables.TipoAsiento
                && a.Concepto == concepto, ct);
        if (asiento is not null)
        {
            // Revierte el efecto de la póliza de cierre antes de eliminarla (operación inversa
            // de RegistrarAsientoAsync: el débito sumaba saldo y el crédito lo restaba).
            var cuentaIds = asiento.Detalles.Select(d => d.CuentaId).ToList();
            var cuentas = await db.CuentasContables
                .Where(c => cuentaIds.Contains(c.Id)).ToListAsync(ct);
            foreach (var d in asiento.Detalles)
            {
                var cuenta = cuentas.FirstOrDefault(c => c.Id == d.CuentaId);
                if (cuenta is null) continue;
                cuenta.SaldoActual = d.TipoMovimiento == MovimientoTipo.Debito
                    ? cuenta.SaldoActual - d.Importe
                    : cuenta.SaldoActual + d.Importe;
            }
            db.DetallesAsientos.RemoveRange(asiento.Detalles);
            db.AsientosContables.Remove(asiento);
        }
        db.PeriodosContables.Remove(flag);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Costo de ventas registrado en la póliza de cierre del periodo (0 si no la tiene).</summary>
    private async Task<decimal> CostoDelCierreAsync(int suc, int anio, int mes, CancellationToken ct)
    {
        var concepto = CierresContables.ConceptoCierre(anio, mes);
        return await db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable!.SucursalId == suc
                && d.AsientoContable.Tipo == CierresContables.TipoAsiento
                && d.AsientoContable.Concepto == concepto
                && d.TipoMovimiento == MovimientoTipo.Debito)
            .SumAsync(d => d.Importe, ct);
    }

    private static void ValidarPeriodo(int anio, int mes)
    {
        if (mes is < 1 or > 12) throw new InvalidOperationException("El mes debe estar entre 1 y 12.");
        if (anio < 1900 || anio > 9999) throw new InvalidOperationException("El año no es válido.");
    }

    /// <summary>
    /// Normaliza una fecha de negocio a hora local para derivar el (año, mes) del periodo.
    /// Unspecified se trata como local (convención de todo el ERP: fechas de UI = hora local).
    /// </summary>
    private static DateTime ALocal(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha.ToLocalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Local)
    };
}
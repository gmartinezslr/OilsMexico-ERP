using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Gastos operativos: alta/edición/baja + listado por período/categoría.
/// REGLA #1: toda lectura/escritura queda fijada a la sucursal autorizada.
/// Prioridad 1.1: el alta genera póliza automática → respeta periodo cerrado.
/// Prioridad 1.2: alta/edición/baja registran auditoría (quién/cuándo/valores).
/// </summary>
public sealed partial class GastoService(
    ErpDbContext db, ISucursalContext ctx,
    ICuentasContablesService cuentas, ICierreContableService cierre) : IGastoService
{
    private static readonly string[] FormasValidas = ["01", "02", "03", "04", "28", "99"];

    public async Task<GastoResult> CrearAsync(CrearGastoRequest req, CancellationToken ct = default)
    {
        ExigirActorAuditoria();
        var suc = ctx.ResolverSucursalAutorizada(req.SucursalId);
        var (categoria, concepto) = Validar(req);
        var fechaGasto = NormalizarFecha(req.Fecha);
        // Prioridad 1.1: el gasto genera póliza (libro); la fecha OPERATIVA manda, no la de captura.
        var fechaLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(fechaGasto);
        if (await cierre.EstaCerradoAsync(suc, fechaLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(fechaLocal));
        var gasto = new Gasto
        {
            SucursalId = suc,
            Fecha = fechaGasto,
            Categoria = categoria,
            Concepto = concepto,
            Importe = Math.Round(req.Importe, 2),
            FormaPagoSat = req.FormaPagoSat,
            Comprobante = string.IsNullOrWhiteSpace(req.Comprobante) ? null : req.Comprobante.Trim(),
            Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim(),
            UsuarioId = ctx.UsuarioId
        };
        await using var transaccion = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            db.Gastos.Add(gasto);
            await db.SaveChangesAsync(ct);
            await RegistrarPolizaAsync(gasto, ct);
            RegistrarAuditoria("GastoCreado",
                $"Gasto #{gasto.Id} {gasto.Categoria} ${gasto.Importe:N2} ({gasto.Concepto}) sucursal {gasto.SucursalId}.",
                null, $"${gasto.Importe:N2}");
            await db.SaveChangesAsync(ct);
            if (transaccion is not null) await transaccion.CommitAsync(ct);
        }
        catch
        {
            if (transaccion is not null)
                await transaccion.RollbackAsync(CancellationToken.None);
            else if (db.Entry(gasto).State != EntityState.Detached)
            {
                foreach (var entry in db.ChangeTracker.Entries<AsientoContable>()
                    .Where(e => e.State == EntityState.Added))
                    entry.State = EntityState.Detached;
                db.Gastos.Remove(gasto);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            throw;
        }
        return new GastoResult(gasto.Id, gasto.Fecha, gasto.Categoria, gasto.Concepto,
            gasto.Importe, gasto.FormaPagoSat, gasto.Comprobante, "CREADO");
    }

    public async Task<GastoResult> EditarAsync(int gastoId, CrearGastoRequest req, CancellationToken ct = default)
    {
        ExigirActorAuditoria();
        var gasto = await db.Gastos.FirstOrDefaultAsync(g => g.Id == gastoId, ct)
            ?? throw new InvalidOperationException("Gasto no existe.");
        if (!ctx.EsAdmin && gasto.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes editar gastos de otra sucursal.");
        var (categoria, concepto) = Validar(req);
        var fechaNueva = NormalizarFecha(req.Fecha);
        var fechaAnteriorLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(gasto.Fecha);
        var fechaNuevaLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(fechaNueva);
        if (await cierre.EstaCerradoAsync(gasto.SucursalId, fechaAnteriorLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(fechaAnteriorLocal));
        if (await cierre.EstaCerradoAsync(gasto.SucursalId, fechaNuevaLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(fechaNuevaLocal));
        var cambiosContables = categoria != gasto.Categoria || concepto != gasto.Concepto
            || Math.Round(req.Importe, 2) != gasto.Importe || fechaNueva != gasto.Fecha
            || req.FormaPagoSat != gasto.FormaPagoSat;
        if (cambiosContables && await TienePolizaRegistradaAsync(gasto, ct))
            throw new InvalidOperationException(
                "El gasto ya tiene una póliza contable; no se pueden cambiar sus datos contables. Registra un ajuste contable.");
        var antes = $"{gasto.Categoria}|{gasto.Concepto}|${gasto.Importe:N2}|{gasto.Fecha:yyyy-MM-dd}";
        gasto.Fecha = fechaNueva;
        gasto.Categoria = categoria;
        gasto.Concepto = concepto;
        gasto.Importe = Math.Round(req.Importe, 2);
        gasto.FormaPagoSat = req.FormaPagoSat;
        gasto.Comprobante = string.IsNullOrWhiteSpace(req.Comprobante) ? null : req.Comprobante.Trim();
        gasto.Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim();
        // Prioridad 1.2: trazabilidad de edición (antes/después).
        var despues = $"{gasto.Categoria}|{gasto.Concepto}|${gasto.Importe:N2}|{gasto.Fecha:yyyy-MM-dd}";
        RegistrarAuditoria("GastoActualizado", $"Gasto #{gasto.Id} editado.", antes, despues);
        await db.SaveChangesAsync(ct);
        return new GastoResult(gasto.Id, gasto.Fecha, gasto.Categoria, gasto.Concepto,
            gasto.Importe, gasto.FormaPagoSat, gasto.Comprobante, "ACTUALIZADO");
    }

    public async Task EliminarAsync(int gastoId, CancellationToken ct = default)
    {
        ExigirActorAuditoria();
        var gasto = await db.Gastos.FirstOrDefaultAsync(g => g.Id == gastoId, ct)
            ?? throw new InvalidOperationException("Gasto no existe.");
        if (!ctx.EsAdmin && gasto.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes eliminar gastos de otra sucursal.");
        var fechaLocal = OilsMexico.Domain.Services.ReglaPeriodoCerrado.ALocal(gasto.Fecha);
        if (await cierre.EstaCerradoAsync(gasto.SucursalId, fechaLocal, ct))
            throw new InvalidOperationException(
                OilsMexico.Domain.Services.ReglaPeriodoCerrado.Mensaje(fechaLocal));
        if (await TienePolizaRegistradaAsync(gasto, ct))
            throw new InvalidOperationException(
                "El gasto ya tiene una póliza contable y no se puede eliminar. Registra un ajuste contable.");
        var detalle = $"Gasto #{gasto.Id} {gasto.Categoria} ${gasto.Importe:N2} ({gasto.Concepto}).";
        db.Gastos.Remove(gasto);
        // Solo se elimina si no existe póliza; los gastos contabilizados requieren un ajuste.
        RegistrarAuditoria("GastoEliminado", detalle, $"${gasto.Importe:N2}", "eliminado");
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<GastoListadoDto>> ListarAsync(
        int sucursalId, DateTime desde, DateTime hasta, string? categoria, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var (ini, fin) = Rango(desde, hasta);
        var baseQ = db.Gastos.AsNoTracking()
            .Where(g => g.SucursalId == suc && g.Fecha >= ini && g.Fecha < fin);
        if (!string.IsNullOrWhiteSpace(categoria))
            baseQ = baseQ.Where(g => g.Categoria == categoria.Trim());
        var filas = await baseQ.OrderByDescending(g => g.Fecha).ThenByDescending(g => g.Id)
            .ToListAsync(ct);
        var nombres = await db.Usuarios.AsNoTracking()
            .Where(u => filas.Select(g => g.UsuarioId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        return filas.Select(g => new GastoListadoDto(
            g.Id, g.Fecha, g.Categoria, g.Concepto, g.Importe, g.FormaPagoSat,
            g.Comprobante, g.Notas, g.SucursalId, g.UsuarioId,
            nombres.TryGetValue(g.UsuarioId, out var n) ? n : $"Usuario #{g.UsuarioId}")).ToList();
    }

    public async Task<List<GastoTotalCategoriaDto>> TotalesPorCategoriaAsync(
        int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var (ini, fin) = Rango(desde, hasta);
        return await db.Gastos.AsNoTracking()
            .Where(g => g.SucursalId == suc && g.Fecha >= ini && g.Fecha < fin)
            .GroupBy(g => g.Categoria)
            .Select(g => new GastoTotalCategoriaDto(
                g.Key, Math.Round(g.Sum(x => x.Importe), 2), g.Count()))
            .OrderByDescending(g => g.Total)
            .ToListAsync(ct);
    }

    private Task<bool> TienePolizaRegistradaAsync(Gasto gasto, CancellationToken ct)
    {
        var concepto = $"Gasto {gasto.Categoria} #{gasto.Id} ";
        return db.AsientosContables.AsNoTracking().AnyAsync(a =>
            a.SucursalId == gasto.SucursalId && a.Concepto != null
            && a.Concepto.StartsWith(concepto), ct);
    }
}

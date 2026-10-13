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
/// </summary>
public sealed partial class GastoService(
    ErpDbContext db, ISucursalContext ctx,
    ICuentasContablesService cuentas) : IGastoService
{
    private static readonly string[] FormasValidas = ["01", "02", "03", "04", "28", "99"];

    public async Task<GastoResult> CrearAsync(CrearGastoRequest req, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(req.SucursalId);
        var (categoria, concepto) = Validar(req);
        var gasto = new Gasto
        {
            SucursalId = suc,
            Fecha = NormalizarFecha(req.Fecha),
            Categoria = categoria,
            Concepto = concepto,
            Importe = Math.Round(req.Importe, 2),
            FormaPagoSat = req.FormaPagoSat,
            Comprobante = string.IsNullOrWhiteSpace(req.Comprobante) ? null : req.Comprobante.Trim(),
            Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim(),
            UsuarioId = ctx.UsuarioId
        };
        db.Gastos.Add(gasto);
        await db.SaveChangesAsync(ct);
        await RegistrarPolizaAsync(gasto, ct);
        return new GastoResult(gasto.Id, gasto.Fecha, gasto.Categoria, gasto.Concepto,
            gasto.Importe, gasto.FormaPagoSat, gasto.Comprobante, "CREADO");
    }

    public async Task<GastoResult> EditarAsync(int gastoId, CrearGastoRequest req, CancellationToken ct = default)
    {
        var gasto = await db.Gastos.FirstOrDefaultAsync(g => g.Id == gastoId, ct)
            ?? throw new InvalidOperationException("Gasto no existe.");
        if (!ctx.EsAdmin && gasto.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes editar gastos de otra sucursal.");
        var (categoria, concepto) = Validar(req);
        gasto.Fecha = NormalizarFecha(req.Fecha);
        gasto.Categoria = categoria;
        gasto.Concepto = concepto;
        gasto.Importe = Math.Round(req.Importe, 2);
        gasto.FormaPagoSat = req.FormaPagoSat;
        gasto.Comprobante = string.IsNullOrWhiteSpace(req.Comprobante) ? null : req.Comprobante.Trim();
        gasto.Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim();
        await db.SaveChangesAsync(ct);
        return new GastoResult(gasto.Id, gasto.Fecha, gasto.Categoria, gasto.Concepto,
            gasto.Importe, gasto.FormaPagoSat, gasto.Comprobante, "ACTUALIZADO");
    }

    public async Task EliminarAsync(int gastoId, CancellationToken ct = default)
    {
        var gasto = await db.Gastos.FirstOrDefaultAsync(g => g.Id == gastoId, ct)
            ?? throw new InvalidOperationException("Gasto no existe.");
        if (!ctx.EsAdmin && gasto.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes eliminar gastos de otra sucursal.");
        db.Gastos.Remove(gasto);
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
}

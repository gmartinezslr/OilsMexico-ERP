using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Corte / arqueo de caja por turno y sucursal.
/// Un solo corte Abierto por sucursal: apertura con fondo inicial,
/// foto del sistema (ventas no canceladas desde la apertura, por forma de pago)
/// y cierre con conteo físico de efectivo.
/// </summary>
public sealed partial class CorteCajaService(ErpDbContext db, ISucursalContext ctx) : ICorteCajaService
{
    public async Task<CorteAbiertoDto?> AbiertoAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        return await db.CortesCaja.AsNoTracking()
            .Where(c => c.SucursalId == suc && c.Estado == "Abierto")
            .OrderByDescending(c => c.FechaAperturaUtc)
            .Select(c => new CorteAbiertoDto(
                c.Id, c.SucursalId, c.FechaAperturaUtc, c.UsuarioAperturaNombre, c.FondoInicial))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CorteAbiertoDto> AbrirAsync(int sucursalId, decimal fondoInicial, CancellationToken ct = default)
    {
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        if (fondoInicial < 0) throw new InvalidOperationException("El fondo inicial no puede ser negativo.");
        var existe = await db.CortesCaja
            .AnyAsync(c => c.SucursalId == suc && c.Estado == "Abierto", ct);
        if (existe) throw new InvalidOperationException("Ya hay un corte abierto; ciérralo antes de abrir otro.");
        var usuario = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == ctx.UsuarioId).Select(u => u.Nombre).FirstOrDefaultAsync(ct)
            ?? $"Usuario #{ctx.UsuarioId}";
        var corte = new CorteCaja
        {
            SucursalId = suc, UsuarioAperturaId = ctx.UsuarioId,
            UsuarioAperturaNombre = usuario, FechaAperturaUtc = DateTime.UtcNow,
            FondoInicial = Math.Round(fondoInicial, 2), Estado = "Abierto"
        };
        db.CortesCaja.Add(corte);
        await db.SaveChangesAsync(ct);
        return new CorteAbiertoDto(corte.Id, corte.SucursalId, corte.FechaAperturaUtc,
            corte.UsuarioAperturaNombre, corte.FondoInicial);
    }
}

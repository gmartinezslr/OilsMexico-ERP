using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Catálogos de solo lectura para los combos de las páginas (sucursales, clientes,
/// proveedores, vendedores). Elimina el acceso directo a <c>ErpDbContext</c> desde la UI
/// (DIP) y devuelve <see cref="OpcionDto"/> en lugar de entidades del ORM.
/// </summary>
public sealed class CatalogoConsultaService(ErpDbContext db) : ICatalogoConsultaService
{
    public Task<List<OpcionDto>> SucursalesAsync(CancellationToken ct = default) =>
        db.Sucursales.AsNoTracking()
            .OrderBy(s => s.Nombre)
            .Select(s => new OpcionDto(s.Id, s.Nombre))
            .ToListAsync(ct);

    public Task<List<OpcionDto>> ClientesAsync(bool soloActivos = true, CancellationToken ct = default)
    {
        var q = db.Clientes.AsNoTracking().AsQueryable();
        if (soloActivos) q = q.Where(c => c.Activo);
        return q.OrderBy(c => c.Nombre)
            .Select(c => new OpcionDto(c.Id, c.Nombre))
            .ToListAsync(ct);
    }

    public Task<List<OpcionDto>> ProveedoresAsync(bool soloActivos = true, CancellationToken ct = default)
    {
        var q = db.Proveedores.AsNoTracking().AsQueryable();
        if (soloActivos) q = q.Where(p => p.Activo);
        return q.OrderBy(p => p.Nombre)
            .Select(p => new OpcionDto(p.Id, p.Nombre))
            .ToListAsync(ct);
    }

    public Task<List<OpcionDto>> VendedoresAsync(CancellationToken ct = default) =>
        db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && (u.Rol == Roles.Vendedor || u.Rol == Roles.Admin))
            .OrderBy(u => u.Nombre)
            .Select(u => new OpcionDto(u.Id, u.Nombre))
            .ToListAsync(ct);

    public async Task<Dictionary<int, int>> VendedorPorClienteAsync(CancellationToken ct = default) =>
        await db.Clientes.AsNoTracking()
            .Where(c => c.VendedorId != null)
            .Select(c => new { c.Id, VendedorId = c.VendedorId!.Value })
            .ToDictionaryAsync(x => x.Id, x => x.VendedorId, ct);

    public Task<List<int>> SucursalIdsAsync(CancellationToken ct = default) =>
        db.Sucursales.AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(ct);
}

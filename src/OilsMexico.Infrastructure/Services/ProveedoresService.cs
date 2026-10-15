using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Padrón de proveedores: antes la página Blazor ejecutaba LINQ y <c>SaveChanges</c> contra
/// <c>ErpDbContext</c> directo; ahora la web sólo depende de <see cref="IProveedoresService"/>
/// (DIP §6.2). Al persistir selectivamente, campos no editados en la UI (p. ej.
/// <c>DiasCredito</c>) conservan su valor en BD en lugar de reiniciarse como ocurría
/// al guardar la entidad clonada a mano desde la página.
/// </summary>
public sealed class ProveedoresService(ErpDbContext db) : IProveedoresService
{
    public async Task<List<ProveedorCatalogoDto>> ListarAsync(string? filtro, bool soloActivos = true, CancellationToken ct = default)
    {
        var q = db.Proveedores.AsNoTracking().AsQueryable();
        if (soloActivos) q = q.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(p => p.Nombre.ToLower().Contains(f) || p.Rfc.ToLower().Contains(f));
        }
        return await q.OrderBy(p => p.Nombre).Take(200).Select(p => D(p)).ToListAsync(ct);
    }

    public async Task<ProveedorCatalogoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Proveedores.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : D(p);
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarAsync(ProveedorCatalogoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Rfc))
            return (false, "Nombre y RFC son obligatorios.", 0);

        var direccion = $"{dto.Calle} {dto.NumeroExterior} {dto.NumeroInterior}, {dto.Colonia}, CP {dto.CodigoPostal}, {dto.Municipio}, {dto.Estado}".Trim(' ', ',');

        if (dto.Id == 0)
        {
            var p = new Proveedor
            {
                Nombre = dto.Nombre, Rfc = dto.Rfc, Telefono = dto.Telefono, Email = dto.Email,
                Calle = dto.Calle, NumeroExterior = dto.NumeroExterior, NumeroInterior = dto.NumeroInterior,
                Colonia = dto.Colonia, CodigoPostal = dto.CodigoPostal, Municipio = dto.Municipio,
                Estado = dto.Estado, Ciudad = dto.Ciudad, Pais = dto.Pais, Direccion = direccion,
                Activo = dto.Activo, DiasCredito = dto.DiasCredito, SituacionFiscal = dto.SituacionFiscal
            };
            db.Proveedores.Add(p);
            await db.SaveChangesAsync(ct);
            return (true, $"Proveedor '{p.Nombre}' guardado.", p.Id);
        }

        var upd = await db.Proveedores.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "El proveedor ya no existe.", 0);
        upd.Nombre = dto.Nombre; upd.Rfc = dto.Rfc; upd.Telefono = dto.Telefono; upd.Email = dto.Email;
        upd.Calle = dto.Calle; upd.NumeroExterior = dto.NumeroExterior; upd.NumeroInterior = dto.NumeroInterior;
        upd.Colonia = dto.Colonia; upd.CodigoPostal = dto.CodigoPostal; upd.Municipio = dto.Municipio;
        upd.Estado = dto.Estado; upd.Ciudad = dto.Ciudad; upd.Pais = dto.Pais; upd.Direccion = direccion;
        upd.SituacionFiscal = dto.SituacionFiscal; // DiasCredito no se edita en la UI: se conserva.
        await db.SaveChangesAsync(ct);
        return (true, $"Proveedor '{upd.Nombre}' guardado.", upd.Id);
    }

    public async Task<(bool Ok, string Mensaje)> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Proveedores.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return (false, "El proveedor ya no existe.");
        p.Activo = false;
        await db.SaveChangesAsync(ct);
        return (true, "Proveedor desactivado.");
    }

    private static ProveedorCatalogoDto D(Proveedor p) => new()
    {
        Id = p.Id, Nombre = p.Nombre, Rfc = p.Rfc, Telefono = p.Telefono, Email = p.Email,
        Calle = p.Calle, NumeroExterior = p.NumeroExterior, NumeroInterior = p.NumeroInterior,
        Colonia = p.Colonia, CodigoPostal = p.CodigoPostal, Municipio = p.Municipio,
        Estado = p.Estado, Ciudad = p.Ciudad, Pais = p.Pais, Direccion = p.Direccion,
        Activo = p.Activo, DiasCredito = p.DiasCredito, SituacionFiscal = p.SituacionFiscal
    };
}
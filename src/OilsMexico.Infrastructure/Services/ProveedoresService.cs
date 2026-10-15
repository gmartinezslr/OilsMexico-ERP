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
        var q = db.Proveedores.AsNoTracking()
            .Include(p => p.Contactos).Include(p => p.Correos)
            .Include(p => p.Telefonos).Include(p => p.Direcciones)
            .AsSplitQuery()
            .AsQueryable();
        if (soloActivos) q = q.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(p => p.Nombre.ToLower().Contains(f) || p.Rfc.ToLower().Contains(f));
        }
        var proveedores = await q.OrderBy(p => p.Nombre).Take(200).ToListAsync(ct);
        return proveedores.Select(D).ToList();
    }

    public async Task<ProveedorCatalogoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Proveedores.AsNoTracking()
            .Include(x => x.Contactos).Include(x => x.Correos)
            .Include(x => x.Telefonos).Include(x => x.Direcciones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : D(p);
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarAsync(ProveedorCatalogoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Rfc))
            return (false, "Nombre y RFC son obligatorios.", 0);

        var validacion = TerceroContactosMapping.Validar(dto.Contactos, dto.Correos, dto.Telefonos, dto.Direcciones, false);
        if (validacion is not null) return (false, validacion, 0);

        if (dto.Id == 0)
        {
            var nuevos = TerceroContactosMapping.CrearEntidades(dto.Contactos, dto.Correos, dto.Telefonos, dto.Direcciones, null, null);
            var direccionPrincipal = PrincipalDireccion(dto);
            var p = new Proveedor
            {
                Nombre = dto.Nombre.Trim(), Rfc = dto.Rfc.Trim().ToUpperInvariant(),
                Telefono = PrincipalTelefono(dto), Email = PrincipalCorreo(dto),
                Calle = direccionPrincipal?.Calle, NumeroExterior = direccionPrincipal?.NumeroExterior,
                NumeroInterior = direccionPrincipal?.NumeroInterior, Colonia = direccionPrincipal?.Colonia,
                CodigoPostal = direccionPrincipal?.CodigoPostal ?? dto.CodigoPostal, Municipio = direccionPrincipal?.Municipio,
                Estado = direccionPrincipal?.Estado, Ciudad = direccionPrincipal?.Ciudad,
                Pais = direccionPrincipal?.Pais ?? dto.Pais,
                Direccion = FormatearDireccion(direccionPrincipal),
                Activo = dto.Activo, DiasCredito = dto.DiasCredito, SituacionFiscal = dto.SituacionFiscal,
                Contactos = nuevos.Contactos, Correos = nuevos.Correos,
                Telefonos = nuevos.Telefonos, Direcciones = nuevos.Direcciones
            };
            db.Proveedores.Add(p);
            await db.SaveChangesAsync(ct);
            return (true, $"Proveedor '{p.Nombre}' guardado.", p.Id);
        }

        var upd = await db.Proveedores
            .Include(x => x.Contactos).Include(x => x.Correos)
            .Include(x => x.Telefonos).Include(x => x.Direcciones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "El proveedor ya no existe.", 0);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.RemoveRange(upd.Correos); db.RemoveRange(upd.Telefonos);
        db.RemoveRange(upd.Direcciones); db.RemoveRange(upd.Contactos);
        await db.SaveChangesAsync(ct);

        var actualizados = TerceroContactosMapping.CrearEntidades(dto.Contactos, dto.Correos, dto.Telefonos, dto.Direcciones, null, upd.Id);
        upd.Contactos = actualizados.Contactos; upd.Correos = actualizados.Correos;
        upd.Telefonos = actualizados.Telefonos; upd.Direcciones = actualizados.Direcciones;
        var direccionPrincipalActualizada = PrincipalDireccion(dto);
        upd.Nombre = dto.Nombre.Trim(); upd.Rfc = dto.Rfc.Trim().ToUpperInvariant();
        upd.Telefono = PrincipalTelefono(dto); upd.Email = PrincipalCorreo(dto);
        upd.Calle = direccionPrincipalActualizada?.Calle; upd.NumeroExterior = direccionPrincipalActualizada?.NumeroExterior;
        upd.NumeroInterior = direccionPrincipalActualizada?.NumeroInterior; upd.Colonia = direccionPrincipalActualizada?.Colonia;
        upd.CodigoPostal = direccionPrincipalActualizada?.CodigoPostal ?? dto.CodigoPostal; upd.Municipio = direccionPrincipalActualizada?.Municipio;
        upd.Estado = direccionPrincipalActualizada?.Estado; upd.Ciudad = direccionPrincipalActualizada?.Ciudad;
        upd.Pais = direccionPrincipalActualizada?.Pais ?? dto.Pais;
        upd.Direccion = FormatearDireccion(direccionPrincipalActualizada);
        upd.SituacionFiscal = dto.SituacionFiscal; // DiasCredito no se edita en la UI: se conserva.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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

    private static ProveedorCatalogoDto D(Proveedor p)
    {
        var dto = new ProveedorCatalogoDto
        {
        Id = p.Id, Nombre = p.Nombre, Rfc = p.Rfc, Telefono = p.Telefono, Email = p.Email,
        Calle = p.Calle, NumeroExterior = p.NumeroExterior, NumeroInterior = p.NumeroInterior,
        Colonia = p.Colonia, CodigoPostal = p.CodigoPostal, Municipio = p.Municipio,
        Estado = p.Estado, Ciudad = p.Ciudad, Pais = p.Pais, Direccion = p.Direccion,
        Activo = p.Activo, DiasCredito = p.DiasCredito, SituacionFiscal = p.SituacionFiscal
        };
        TerceroContactosMapping.CargarEnDto(p.Contactos, p.Correos, p.Telefonos, p.Direcciones, dto);
        return dto;
    }

    private static string? PrincipalCorreo(ProveedorCatalogoDto dto)
        => dto.Correos.FirstOrDefault(x => x.EsPrincipal)?.Direccion ?? dto.Correos.FirstOrDefault()?.Direccion;
    private static string? PrincipalTelefono(ProveedorCatalogoDto dto)
        => dto.Telefonos.FirstOrDefault(x => x.EsPrincipal)?.Numero ?? dto.Telefonos.FirstOrDefault()?.Numero;
    private static DireccionTerceroDto? PrincipalDireccion(ProveedorCatalogoDto dto)
        => dto.Direcciones.FirstOrDefault(x => x.EsPredeterminada && x.Tipo.Equals("Fiscal", StringComparison.OrdinalIgnoreCase))
            ?? dto.Direcciones.FirstOrDefault(x => x.EsPredeterminada && x.Tipo.Equals("Entrega", StringComparison.OrdinalIgnoreCase))
            ?? dto.Direcciones.FirstOrDefault(x => x.EsPredeterminada)
            ?? dto.Direcciones.FirstOrDefault();
    private static string? FormatearDireccion(DireccionTerceroDto? d)
        => d is null ? null : $"{d.Calle} {d.NumeroExterior} {d.NumeroInterior}, {d.Colonia}, CP {d.CodigoPostal}, {d.Municipio}, {d.Estado}".Trim(' ', ',');
}
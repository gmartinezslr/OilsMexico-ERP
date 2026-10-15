using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Padrón de clientes: antes la página Blazor ejecutaba LINQ y <c>SaveChanges</c> contra
/// <c>ErpDbContext</c> directo; ahora la web sólo depende de <see cref="IClientesService"/>
/// (DIP §6.2). El servicio valida lo básico (nombre/RFC) y compone el domicilio; las
/// validaciones con feedback en vivo (RFC del SAT, situación fiscal, colonia SEPOMEX)
/// siguen en la página porque son puras y no tocan la BD.
/// </summary>
public sealed class ClientesService(ErpDbContext db) : IClientesService
{
    public async Task<List<ClienteCatalogoDto>> ListarAsync(string? filtro, bool soloActivos = true, CancellationToken ct = default)
    {
        var q = db.Clientes.AsNoTracking()
            .Include(c => c.Contactos).Include(c => c.Correos)
            .Include(c => c.Telefonos).Include(c => c.Direcciones)
            .AsSplitQuery()
            .AsQueryable();
        if (soloActivos) q = q.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(c => c.Nombre.ToLower().Contains(f) || c.Rfc.ToLower().Contains(f));
        }
        var clientes = await q.OrderBy(c => c.Nombre).Take(200).ToListAsync(ct);
        return clientes.Select(D).ToList();
    }

    public async Task<List<ClienteVentaDto>> ListarParaVentaAsync(CancellationToken ct = default)
        => await db.Clientes.AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new ClienteVentaDto(c.Id, c.Nombre, c.Rfc, c.VendedorId))
            .ToListAsync(ct);

    public async Task<ClienteCatalogoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var c = await db.Clientes.AsNoTracking()
            .Include(x => x.Contactos).Include(x => x.Correos)
            .Include(x => x.Telefonos).Include(x => x.Direcciones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : D(c);
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarAsync(ClienteCatalogoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Rfc))
            return (false, "Nombre y RFC son obligatorios.", 0);

        var validacion = TerceroContactosMapping.Validar(dto.Contactos, dto.Correos, dto.Telefonos, dto.Direcciones, true);
        if (validacion is not null) return (false, validacion, 0);

        if (dto.Id == 0)
        {
            var nuevos = TerceroContactosMapping.CrearEntidades(dto.Contactos, dto.Correos, dto.Telefonos, dto.Direcciones, null, null);
            var direccionPrincipal = PrincipalDireccion(dto);
            var c = new Cliente
            {
                Nombre = dto.Nombre.Trim(), Rfc = dto.Rfc.Trim().ToUpperInvariant(),
                Telefono = PrincipalTelefono(dto), Email = PrincipalCorreo(dto),
                Direccion = FormatearDireccion(direccionPrincipal),
                Calle = direccionPrincipal?.Calle, NumeroExterior = direccionPrincipal?.NumeroExterior,
                NumeroInterior = direccionPrincipal?.NumeroInterior, Colonia = direccionPrincipal?.Colonia,
                Municipio = direccionPrincipal?.Municipio, Estado = direccionPrincipal?.Estado,
                Ciudad = direccionPrincipal?.Ciudad, Pais = direccionPrincipal?.Pais ?? dto.Pais, Activo = dto.Activo,
                DiasCredito = dto.DiasCredito, LimiteCredito = dto.LimiteCredito,
                TipoPrecio = dto.TipoPrecio, RegimenFiscal = dto.RegimenFiscal,
                CodigoPostal = direccionPrincipal?.CodigoPostal ?? dto.CodigoPostal, VendedorId = dto.VendedorId,
                SituacionFiscal = dto.SituacionFiscal,
                Contactos = nuevos.Contactos, Correos = nuevos.Correos,
                Telefonos = nuevos.Telefonos, Direcciones = nuevos.Direcciones
            };
            db.Clientes.Add(c);
            await db.SaveChangesAsync(ct);
            return (true, $"Cliente '{c.Nombre}' guardado.", c.Id);
        }

        var upd = await db.Clientes
            .Include(x => x.Contactos).Include(x => x.Correos)
            .Include(x => x.Telefonos).Include(x => x.Direcciones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "El cliente ya no existe.", 0);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.RemoveRange(upd.Correos); db.RemoveRange(upd.Telefonos);
        db.RemoveRange(upd.Direcciones); db.RemoveRange(upd.Contactos);
        await db.SaveChangesAsync(ct);

        var actualizados = TerceroContactosMapping.CrearEntidades(dto.Contactos, dto.Correos, dto.Telefonos, dto.Direcciones, upd.Id, null);
        upd.Contactos = actualizados.Contactos; upd.Correos = actualizados.Correos;
        upd.Telefonos = actualizados.Telefonos; upd.Direcciones = actualizados.Direcciones;
        upd.Nombre = dto.Nombre.Trim(); upd.Rfc = dto.Rfc.Trim().ToUpperInvariant();
        upd.Telefono = PrincipalTelefono(dto); upd.Email = PrincipalCorreo(dto);
        var direccion = PrincipalDireccion(dto);
        upd.Direccion = FormatearDireccion(direccion);
        upd.Calle = direccion?.Calle; upd.NumeroExterior = direccion?.NumeroExterior;
        upd.NumeroInterior = direccion?.NumeroInterior; upd.Colonia = direccion?.Colonia;
        upd.Municipio = direccion?.Municipio; upd.Estado = direccion?.Estado;
        upd.Ciudad = direccion?.Ciudad; upd.Pais = direccion?.Pais ?? dto.Pais;
        upd.DiasCredito = dto.DiasCredito; upd.LimiteCredito = dto.LimiteCredito;
        upd.TipoPrecio = dto.TipoPrecio; upd.RegimenFiscal = dto.RegimenFiscal;
        upd.CodigoPostal = direccion?.CodigoPostal ?? dto.CodigoPostal; upd.VendedorId = dto.VendedorId;
        upd.SituacionFiscal = dto.SituacionFiscal;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (true, $"Cliente '{upd.Nombre}' guardado.", upd.Id);
    }

    public async Task<(bool Ok, string Mensaje)> DesactivarAsync(int id, CancellationToken ct = default)
    {
        var c = await db.Clientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return (false, "El cliente ya no existe.");
        c.Activo = false;
        await db.SaveChangesAsync(ct);
        return (true, "Cliente desactivado.");
    }

    private static ClienteCatalogoDto D(Cliente c)
    {
        var dto = new ClienteCatalogoDto
        {
        Id = c.Id, Nombre = c.Nombre, Rfc = c.Rfc, Telefono = c.Telefono, Email = c.Email,
        Direccion = c.Direccion, Calle = c.Calle, NumeroExterior = c.NumeroExterior,
        NumeroInterior = c.NumeroInterior, Colonia = c.Colonia, Municipio = c.Municipio,
        Estado = c.Estado, Ciudad = c.Ciudad, Pais = c.Pais, Activo = c.Activo,
        DiasCredito = c.DiasCredito, LimiteCredito = c.LimiteCredito, TipoPrecio = c.TipoPrecio,
        RegimenFiscal = c.RegimenFiscal, CodigoPostal = c.CodigoPostal, VendedorId = c.VendedorId,
        SituacionFiscal = c.SituacionFiscal
        };
        TerceroContactosMapping.CargarEnDto(c.Contactos, c.Correos, c.Telefonos, c.Direcciones, dto);
        return dto;
    }

    private static string? PrincipalCorreo(ClienteCatalogoDto dto)
        => dto.Correos.FirstOrDefault(x => x.EsPrincipal)?.Direccion ?? dto.Correos.FirstOrDefault()?.Direccion;
    private static string? PrincipalTelefono(ClienteCatalogoDto dto)
        => dto.Telefonos.FirstOrDefault(x => x.EsPrincipal)?.Numero ?? dto.Telefonos.FirstOrDefault()?.Numero;
    private static DireccionTerceroDto? PrincipalDireccion(ClienteCatalogoDto dto)
        => dto.Direcciones.FirstOrDefault(x => x.EsPredeterminada && x.Tipo.Equals("Fiscal", StringComparison.OrdinalIgnoreCase))
            ?? dto.Direcciones.FirstOrDefault(x => x.EsPredeterminada && x.Tipo.Equals("Entrega", StringComparison.OrdinalIgnoreCase))
            ?? dto.Direcciones.FirstOrDefault(x => x.EsPredeterminada)
            ?? dto.Direcciones.FirstOrDefault();
    private static string? FormatearDireccion(DireccionTerceroDto? d)
        => d is null ? null : $"{d.Calle} {d.NumeroExterior} {d.NumeroInterior}, {d.Colonia}, CP {d.CodigoPostal}, {d.Municipio}, {d.Estado}".Trim(' ', ',');
}
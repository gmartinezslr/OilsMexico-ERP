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
        var q = db.Clientes.AsNoTracking().AsQueryable();
        if (soloActivos) q = q.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(c => c.Nombre.ToLower().Contains(f) || c.Rfc.ToLower().Contains(f));
        }
        return await q.OrderBy(c => c.Nombre).Take(200).Select(c => D(c)).ToListAsync(ct);
    }

    public async Task<ClienteCatalogoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var c = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : D(c);
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarAsync(ClienteCatalogoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Rfc))
            return (false, "Nombre y RFC son obligatorios.", 0);

        var direccion = $"{dto.Calle} {dto.NumeroExterior} {dto.NumeroInterior}, {dto.Colonia}, CP {dto.CodigoPostal}, {dto.Municipio}, {dto.Estado}".Trim(' ', ',');

        if (dto.Id == 0)
        {
            var c = new Cliente
            {
                Nombre = dto.Nombre, Rfc = dto.Rfc, Telefono = dto.Telefono, Email = dto.Email,
                Direccion = direccion, Calle = dto.Calle, NumeroExterior = dto.NumeroExterior,
                NumeroInterior = dto.NumeroInterior, Colonia = dto.Colonia, Municipio = dto.Municipio,
                Estado = dto.Estado, Ciudad = dto.Ciudad, Pais = dto.Pais, Activo = dto.Activo,
                DiasCredito = dto.DiasCredito, LimiteCredito = dto.LimiteCredito,
                TipoPrecio = dto.TipoPrecio, RegimenFiscal = dto.RegimenFiscal,
                CodigoPostal = dto.CodigoPostal, VendedorId = dto.VendedorId,
                SituacionFiscal = dto.SituacionFiscal
            };
            db.Clientes.Add(c);
            await db.SaveChangesAsync(ct);
            return (true, $"Cliente '{c.Nombre}' guardado.", c.Id);
        }

        var upd = await db.Clientes.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "El cliente ya no existe.", 0);
        upd.Nombre = dto.Nombre; upd.Rfc = dto.Rfc; upd.Telefono = dto.Telefono; upd.Email = dto.Email;
        upd.Direccion = direccion; upd.Calle = dto.Calle; upd.NumeroExterior = dto.NumeroExterior;
        upd.NumeroInterior = dto.NumeroInterior; upd.Colonia = dto.Colonia; upd.Municipio = dto.Municipio;
        upd.Estado = dto.Estado; upd.Ciudad = dto.Ciudad; upd.Pais = dto.Pais;
        upd.DiasCredito = dto.DiasCredito; upd.LimiteCredito = dto.LimiteCredito;
        upd.TipoPrecio = dto.TipoPrecio; upd.RegimenFiscal = dto.RegimenFiscal;
        upd.CodigoPostal = dto.CodigoPostal; upd.VendedorId = dto.VendedorId;
        upd.SituacionFiscal = dto.SituacionFiscal;
        await db.SaveChangesAsync(ct);
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

    private static ClienteCatalogoDto D(Cliente c) => new()
    {
        Id = c.Id, Nombre = c.Nombre, Rfc = c.Rfc, Telefono = c.Telefono, Email = c.Email,
        Direccion = c.Direccion, Calle = c.Calle, NumeroExterior = c.NumeroExterior,
        NumeroInterior = c.NumeroInterior, Colonia = c.Colonia, Municipio = c.Municipio,
        Estado = c.Estado, Ciudad = c.Ciudad, Pais = c.Pais, Activo = c.Activo,
        DiasCredito = c.DiasCredito, LimiteCredito = c.LimiteCredito, TipoPrecio = c.TipoPrecio,
        RegimenFiscal = c.RegimenFiscal, CodigoPostal = c.CodigoPostal, VendedorId = c.VendedorId,
        SituacionFiscal = c.SituacionFiscal
    };
}
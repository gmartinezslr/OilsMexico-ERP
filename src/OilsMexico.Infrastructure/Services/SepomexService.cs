using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Implementación SEPOMEX sobre PostgreSQL.
/// Lee el TXT oficial delimitado por '|' (también ',', ';' o TAB) en
/// latin1/UTF-8, con o sin encabezado, y hace upsert por (codigo, asentamiento_id).
/// Orden oficial del archivo (15 columnas):
/// d_codigo|d_asenta|d_tipo_asenta|D_mnpio|d_estado|d_ciudad|d_CP|c_estado|c_oficina|c_CP|c_tipo_asenta|c_mnpio|id_asenta_cpcons|d_zona|c_cve_ciudad
/// </summary>
public sealed partial class SepomexService(ErpDbContext db) : ISepomexService
{
    private const int ColumnasEsperadas = 15;

    public async Task<DireccionSepomexDto?> BuscarPorCpAsync(string codigoPostal, CancellationToken ct = default)
    {
        var cp = (codigoPostal ?? "").Trim();
        if (cp.Length != 5 || !cp.All(char.IsDigit)) return null;
        var filas = await db.CodigosPostales.AsNoTracking()
            .Where(x => x.Codigo == cp)
            .OrderBy(x => x.Asentamiento)
            .Take(100).ToListAsync(ct);
        if (filas.Count == 0) return null;
        var primero = filas[0];
        return new DireccionSepomexDto(cp, primero.Municipio, primero.Estado, primero.Ciudad,
            filas.Select(f => new AsentamientoDto(f.Codigo, f.Asentamiento, f.TipoAsentamiento,
                f.Municipio, f.Estado, f.Ciudad, f.Zona)).ToList());
    }

    public async Task<List<AsentamientoDto>> BuscarColoniasAsync(string texto, int limite = 20, CancellationToken ct = default)
    {
        var t = (texto ?? "").Trim();
        if (t.Length < 2) return [];
        t = t.ToLower();
        return await db.CodigosPostales.AsNoTracking()
            .Where(x => x.Asentamiento.ToLower().Contains(t) || x.Codigo.StartsWith(t))
            .OrderBy(x => x.Codigo).ThenBy(x => x.Asentamiento)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(x => new AsentamientoDto(x.Codigo, x.Asentamiento, x.TipoAsentamiento,
                x.Municipio, x.Estado, x.Ciudad, x.Zona))
            .ToListAsync(ct);
    }

    public async Task<SepomexStatsDto> EstadisticasAsync(CancellationToken ct = default)
    {
        var total = await db.CodigosPostales.LongCountAsync(ct);
        var cps = total == 0 ? 0 : await db.CodigosPostales.Select(x => x.Codigo).Distinct().CountAsync(ct);
        var edos = total == 0 ? 0 : await db.CodigosPostales.Select(x => x.Estado).Distinct().CountAsync(ct);
        return new SepomexStatsDto(total, cps, edos, total == 0 ? null : DateTime.UtcNow);
    }
}

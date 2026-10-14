using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class AuditoriaService(ErpDbContext db, ISucursalContext ctx) : IAuditoriaService
{
    public async Task<List<AuditoriaUsuarioDto>> ListarUsuariosAsync(CancellationToken ct = default)
    {
        ExigirAdmin();
        return await db.Usuarios.AsNoTracking()
            .OrderBy(u => u.Nombre)
            .Select(u => new AuditoriaUsuarioDto(u.Id, u.Nombre))
            .ToListAsync(ct);
    }

    public async Task<List<string>> ListarTiposAsync(CancellationToken ct = default)
    {
        ExigirAdmin();
        return await db.AuditLogs.AsNoTracking()
            .Select(a => a.Tipo)
            .Distinct()
            .OrderBy(tipo => tipo)
            .ToListAsync(ct);
    }

    public async Task<AuditoriaPaginaDto> BuscarAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? usuarioId,
        string? tipo,
        int pagina,
        int porPagina,
        CancellationToken ct = default)
    {
        ExigirAdmin();
        if (hastaUtcExclusivo <= desdeUtc)
            throw new ArgumentException("El final del periodo debe ser posterior al inicio.", nameof(hastaUtcExclusivo));

        var paginaActual = Math.Max(1, pagina);
        var porPaginaReal = Math.Clamp(porPagina, 1, 200);
        var query = db.AuditLogs.AsNoTracking()
            .Where(a => a.FechaUtc >= desdeUtc && a.FechaUtc < hastaUtcExclusivo);

        if (usuarioId.HasValue)
            query = query.Where(a => a.UsuarioId == usuarioId.Value);
        if (!string.IsNullOrWhiteSpace(tipo))
            query = query.Where(a => a.Tipo == tipo);

        var total = await query.CountAsync(ct);
        var registros = await query
            .OrderByDescending(a => a.FechaUtc)
            .ThenByDescending(a => a.Id)
            .Skip((paginaActual - 1) * porPaginaReal)
            .Take(porPaginaReal)
            .Select(a => new AuditLogListadoDto(
                a.Id,
                a.FechaUtc,
                a.UsuarioId,
                a.UsuarioNombre ?? a.Usuario!.Nombre,
                a.Tipo,
                a.Detalle,
                a.Anterior,
                a.Nuevo))
            .ToListAsync(ct);

        return new AuditoriaPaginaDto(registros, total, paginaActual, porPaginaReal);
    }

    private void ExigirAdmin()
    {
        if (!ctx.EsAdmin)
            throw new UnauthorizedAccessException("La consulta de auditoría requiere una sesión Admin.");
    }
}

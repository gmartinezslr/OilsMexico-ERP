using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

public interface IAuditoriaService
{
    Task<List<AuditoriaUsuarioDto>> ListarUsuariosAsync(CancellationToken ct = default);
    Task<List<string>> ListarTiposAsync(CancellationToken ct = default);
    Task<AuditoriaPaginaDto> BuscarAsync(
        DateTime desdeUtc,
        DateTime hastaUtcExclusivo,
        int? usuarioId,
        string? tipo,
        int pagina,
        int porPagina,
        CancellationToken ct = default);
}

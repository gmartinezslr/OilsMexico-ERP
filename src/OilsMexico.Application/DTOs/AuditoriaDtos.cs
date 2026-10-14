namespace OilsMexico.Application.DTOs;

public sealed record AuditoriaUsuarioDto(int Id, string Nombre);

public sealed record AuditLogListadoDto(
    int Id,
    DateTime FechaUtc,
    int UsuarioId,
    string Usuario,
    string Tipo,
    string? Detalle,
    string? Anterior,
    string? Nuevo);

public sealed record AuditoriaPaginaDto(
    IReadOnlyList<AuditLogListadoDto> Registros,
    int Total,
    int Pagina,
    int PorPagina);

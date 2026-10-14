namespace OilsMexico.Application.DTOs;

public sealed record RolPermisoMatrizDto(
    int RolId,
    string Rol,
    int PermisoId,
    string Recurso,
    string Etiqueta,
    bool Permitido,
    bool Fijo);

public sealed record RolPermisoMatrizActualizacionDto(int RolId, IReadOnlyList<int> PermisoIds);

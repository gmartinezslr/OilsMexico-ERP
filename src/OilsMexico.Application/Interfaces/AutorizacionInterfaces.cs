using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

public interface IAutorizacionService
{
    Task<bool> TienePermisoAsync(string permiso, CancellationToken ct = default);
    Task<IReadOnlySet<string>> ObtenerPermisosUsuarioAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RolPermisoMatrizDto>> ObtenerMatrizAsync(CancellationToken ct = default);
    Task GuardarMatrizAsync(
        IReadOnlyList<RolPermisoMatrizActualizacionDto> cambios,
        CancellationToken ct = default);
}

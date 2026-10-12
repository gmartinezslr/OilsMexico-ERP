using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Gestión de credenciales: cambio de contraseña (propia o por administrador) y su
/// política de complejidad/vigencia/historial. Extraído de <c>AuthService</c> para cumplir
/// SRP: la autenticación (login/bloqueo) y la gestión de credenciales cambian por motivos
/// distintos y deben poder evolucionar sin tocarse entre sí.
/// </summary>
public interface ICredencialesService
{
    Task<PasswordConfigDto> ObtenerPasswordConfigAsync(CancellationToken ct = default);
    Task<CambioPasswordResult> GuardarPasswordConfigAsync(PasswordConfigRequest request, int usuarioId, CancellationToken ct = default);
    Task<CambioPasswordResult> CambiarContraseñaAsync(int usuarioId, string contraseñaActual, CambioPasswordRequest request, CancellationToken ct = default);
}

/// <summary>
/// Autenticación de dos factores (2FA) por cuenta: consulta de estado (genera QR/clave
/// pendiente), activación y desactivación. Extraído de <c>AuthService</c> por SRP.
/// </summary>
public interface IDosFaService
{
    Task<DosFaEstadoDto> ObtenerEstadoDosFaAsync(int usuarioId, CancellationToken ct = default);
    Task<CambioPasswordResult> ActivacionDosFaAsync(int usuarioId, ActivarDosFaRequest request, CancellationToken ct = default);
    Task<CambioPasswordResult> DesactivacionDosFaAsync(int usuarioId, DesactivarDosFaRequest request, CancellationToken ct = default);
}

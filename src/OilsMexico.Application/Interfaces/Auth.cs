using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;
public interface IAuthService
{
    Task<LoginResultado> LoginAsync(string identificador, string password, string? codigo2fa = null, CancellationToken ct = default);
    Task<SesionDto?> LoginPorPinAsync(string pin, CancellationToken ct = default);
    Task<bool> DesbloquearUsuarioAsync(int usuarioId, CancellationToken ct = default);

    // -----------------------------------------------------------------------
    // 1) Cambio de contraseña (propio o por cuenta de otro — solo Admin)
    // -----------------------------------------------------------------------
    Task<CambioPasswordResult> CambiarContraseñaAsync(int usuarioId, string contraseñaActual, CambioPasswordRequest request, CancellationToken ct = default);

    // -----------------------------------------------------------------------
    // 2) 2FA (habilitación/deshabilitación opcional por cuenta)
    //    - Habilitar: genera secretario TOTP, QR y clave para el usuario
    //    - Deshabilitar: requiere que el usuario valide el código actual
    //    - Solo Admin puede gestionar el 2FA de otros usuarios
    // -----------------------------------------------------------------------
    Task<DosFaEstadoDto> ObtenerEstadoDosFaAsync(int usuarioId, CancellationToken ct = default);
    Task<CambioPasswordResult> ActivacionDosFaAsync(int usuarioId, ActivarDosFaRequest request, CancellationToken ct = default);
    Task<CambioPasswordResult> DesactivacionDosFaAsync(int usuarioId, DesactivarDosFaRequest request, CancellationToken ct = default);

    // -----------------------------------------------------------------------
    // 3) Política de contraseña (duración, longitud, historial, complejidad)
    //    - La duración se configura por el administrador
    //    - La contraseña no debe igualar ninguna de las últimas N (historial)
    // -----------------------------------------------------------------------
    Task<PasswordConfigDto> ObtenerPasswordConfigAsync(CancellationToken ct = default);
    Task<CambioPasswordResult> GuardarPasswordConfigAsync(PasswordConfigRequest request, int usuarioId, CancellationToken ct = default);

    // -----------------------------------------------------------------------
    // 4) Validación RFC + Situación Fiscal (Clientes y Proveedores)
    // -----------------------------------------------------------------------
    Task<ConsultaRfcResult> ValidarRfcAsync(string rfc, CancellationToken ct = default);
}

public interface ISesionActual
{
    SesionDto? Sesion { get; }
    bool Autenticado { get; }
    Task IniciarAsync(SesionDto sesion);
    /// <summary>
    /// Cierra la sesión borrando el almacenamiento del navegador. Si <paramref name="revocarTokenEnBd"/>
    /// es true, además revoca el token de sesión en la base de datos, lo que invalida también el resto
    /// de sesiones abiertas del mismo usuario en otras pestañas o navegadores (se revalidan contra BD).
    /// </summary>
    Task CerrarAsync(bool revocarTokenEnBd = false);
    Task<bool> RestaurarAsync();
}

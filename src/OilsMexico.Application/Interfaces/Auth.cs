using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResultado> LoginAsync(string identificador, string password, CancellationToken ct = default);
    Task<SesionDto?> LoginPorPinAsync(string pin, CancellationToken ct = default);
    Task<bool> DesbloquearUsuarioAsync(int usuarioId, CancellationToken ct = default);
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

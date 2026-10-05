using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

public interface IAuthService
{
    Task<SesionDto?> LoginPorPinAsync(string pin, CancellationToken ct = default);
}

public interface ISesionActual
{
    SesionDto? Sesion { get; }
    bool Autenticado { get; }
    Task IniciarAsync(SesionDto sesion);
    Task CerrarAsync();
    Task<bool> RestaurarAsync();
}

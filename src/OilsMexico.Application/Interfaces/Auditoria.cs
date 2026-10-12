namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Auditoría del sistema (tabla <c>audit_logs</c>).
///
/// Separada de <see cref="IAuthService"/> por SRP e ISP: <c>AuthService</c> ya tenía la
/// responsabilidad de autenticar, gestionar contraseñas y 2FA y, además, escribir el log
/// de auditoría. Cualquier otro servicio que necesite dejar rastro no debe depender de
/// toda la interfaz de autenticación para lograrlo.
/// </summary>
public interface IAuditoria
{
    /// <summary>
    /// Registra un evento. Si <paramref name="usuarioId"/> no corresponde a un usuario
    /// existente (por ejemplo eventos previos al login) la fila se omite para no romper
    /// el flujo principal, ya que la tabla exige un usuario real (FK).
    /// </summary>
    void Registrar(int usuarioId, string tipo, string? detalle = null, string? anterior = null, string? nuevo = null);
}

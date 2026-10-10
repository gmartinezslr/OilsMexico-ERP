namespace OilsMexico.Domain.Entities;

/// <summary>
/// Historial de contraseñas anteriores. Se almacenan los hashes (nunca el plain text) para que el
/// cambio de contraseña no repita ninguna de las últimas <see cref="OilsMexico.Application.Interfaces.IAuthService.PasswordHistorial"/>
/// configuradas. El uso de <see cref="PasswordHasher{T}"/> de ASP.NET Core Identity garantiza que
/// cada hash es único (sal) y de coste adaptativo, independientemente de que las contraseñas
/// anteriores compartieran bits.
/// </summary>
public sealed class PasswordHistory
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    /// <summary>Hash de Identity (PBKDF2 con sal).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Fecha en que se cambió la contraseña a la que corresponde este hash.</summary>
    public DateTime FechaCambioUtc { get; set; } = DateTime.UtcNow;
}

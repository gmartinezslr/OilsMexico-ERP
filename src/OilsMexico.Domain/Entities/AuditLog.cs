using OilsMexico.Domain.Entities;

namespace OilsMexico.Domain.Entities;

/// <summary>
/// Registro impreso de auditoría: cada cambio de contraseña, habilitación/deshabilitación de 2FA
/// (y sus configuraciones por cuenta), además de cambios de RFC/situación fiscal en clientes y
/// proveedores. No es posible eliminar registros; solo se conservan para trazabilidad legal.
/// </summary>
public sealed class AuditLog
{
    public int Id { get; set; }

    /// <summary>Cuenta operadora (admin o el propio usuario que accionó).</summary>
    public int UsuarioId { get; set; }

    public string? UsuarioNombre { get; set; }

    /// <summary>Clave del evento. Ejemplos: PasswordChange, PasswordHistoryRejected, DosFaActivate, DosFaDeactivate, DosFaConfig, RfcValidated, FiscalSituationChanged, PasswordPolicyChanged.</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Texto libre de detalle (por ejemplo, el RFC validado o el mensaje de error de política).</summary>
    public string? Detalle { get; set; }

    /// <summary>Valor anterior (para cambios de campo).</summary>
    public string? Anterior { get; set; }

    /// <summary>Nuevo valor (para cambios de campo).</summary>
    public string? Nuevo { get; set; }

    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }
}

namespace OilsMexico.Domain.Entities;

/// <summary>Operador del ERP con PIN (heredado del sistema PHP) + sucursal asignada.</summary>
public sealed class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string PinHash { get; set; } = string.Empty; // PIN legacy; la contraseña es la credencial principal
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>REGLA INMUTABLE: cadena heredada (Admin | Vendedor | Almacen | Conta). Se mantiene por compatibilidad; el modelo real es <see cref="RoleId"/>.</summary>
    public string Rol { get; set; } = "Vendedor";
    public int? RoleId { get; set; }
    public Role? Role { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public bool Activo { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public int BloqueosTemporales { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
    public bool BloqueadoDefinitivamente { get; set; }
    /// <summary>Token de sesión vigente; se revoca al desbloquear/desactivar/cambiar contraseña para cerrar sesiones abiertas.</summary>
    public string? SesionToken { get; set; }

    // -----------------------------------------------------------------
    // 2FA (autenticación de dos factores): habilitación/deshabilitación
    // opcional por cuenta. NO se obliga a todos, solo a quien lo active.
    // Los administradores pueden gestionarlo por cuenta de otros usuarios.
    // -----------------------------------------------------------------
    public bool DosFaActivo { get; set; } = false;
    public string? DosFaSecret { get; set; }
    public DateTime? UltimoCambioPasswordUtc { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<PasswordHistory> PasswordHistories { get; set; } = [];
}

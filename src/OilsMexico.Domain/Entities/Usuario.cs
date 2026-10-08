namespace OilsMexico.Domain.Entities;

/// <summary>Operador del ERP con PIN (heredado del sistema PHP) + sucursal asignada.</summary>
public sealed class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string PinHash { get; set; } = string.Empty; // PIN legacy; la contraseña es la credencial principal
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "Vendedor";       // Admin | Vendedor | Almacen | Conta
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public bool Activo { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public int BloqueosTemporales { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
    public bool BloqueadoDefinitivamente { get; set; }
    /// <summary>Token de sesión vigente; se revoca al desbloquear/desactivar/cambiar contraseña para cerrar sesiones abiertas.</summary>
    public string? SesionToken { get; set; }
}

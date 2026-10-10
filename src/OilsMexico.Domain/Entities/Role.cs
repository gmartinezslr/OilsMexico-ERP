namespace OilsMexico.Domain.Entities;

/// <summary>
/// Rol de seguridad del ERP. Reemplaza la cadena <c>Usuario.Rol</c> para cumplir DIP y OCP:
/// agregar roles o permisos no requiere tocar código.
/// </summary>
public sealed class Role
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Admin, Vendedor, Almacen, Conta
    public string Descripcion { get; set; } = string.Empty;
    public ICollection<RolPermiso> RolPermisos { get; set; } = [];
    public ICollection<Usuario> Usuarios { get; set; } = [];
}

/// <summary>
/// Permiso de seguridad (p. ej. Facturar, ConsultarFinanzas). Se asocian a Roles via <c>RolPermiso</c>.
/// </summary>
public sealed class Permiso
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Facturar, ConsultarFinanzas, etc.
    public string Descripcion { get; set; } = string.Empty;
    public ICollection<RolPermiso> RolPermisos { get; set; } = [];
}

/// <summary>Relación muchos a muchos entre <see cref="Role"/> y <see cref="Permiso"/>.</summary>
public sealed class RolPermiso
{
    public int RolId { get; set; }
    public Role Rol { get; set; } = null!;
    public int PermisoId { get; set; }
    public Permiso Permiso { get; set; } = null!;
}

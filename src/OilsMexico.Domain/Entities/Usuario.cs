namespace OilsMexico.Domain.Entities;

/// <summary>Operador del ERP con PIN (heredado del sistema PHP) + sucursal asignada.</summary>
public sealed class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty; // SHA-256 del PIN
    public string Rol { get; set; } = "Vendedor";       // Admin | Vendedor | Almacen | Conta
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public bool Activo { get; set; } = true;
}

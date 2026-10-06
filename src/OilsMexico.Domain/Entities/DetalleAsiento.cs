namespace OilsMexico.Domain.Entities;

/// <summary>
/// Detalle de una póliza contable: una cuenta y dos lados (débito/crédito).
/// </summary>
public sealed class DetalleAsiento
{
    public int Id { get; set; }
    public int AsientoContableId { get; set; }
    public AsientoContable? AsientoContable { get; set; }
    public int CuentaId { get; set; }
    public CuentaContable? Cuenta { get; set; }
    public int TipoMovimiento { get; set; } // 1 Débito | 2 Crédito
    public decimal Importe { get; set; }
    public string? Descripcion { get; set; }
}

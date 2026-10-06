namespace OilsMexico.Domain.Entities;

/// <summary>
/// Catálogo de cuentas contables (Código de cuentas básico).
/// Tipo: 1=Activo, 2=Pasivo, 3=Patrimonio, 4=Ingreso, 5=Gasto.
/// </summary>
public sealed class CuentaContable
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    /// <summary>Código de cuenta (p. ej. 1000, 3000, 4010).</summary>
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Tipo { get; set; } // 1 Activo | 2 Pasivo | 3 Patrimonio | 4 Ingreso | 5 Gasto
    public string? Descripcion { get; set; }
    public decimal SaldoActual { get; set; }
}

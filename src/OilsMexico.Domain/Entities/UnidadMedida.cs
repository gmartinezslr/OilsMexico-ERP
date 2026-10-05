namespace OilsMexico.Domain.Entities;

/// <summary>Equivalencias por presentación. Tabla: unidades_medida.</summary>
public sealed class UnidadMedida
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public string UnidadNombre { get; set; } = "Litro"; // Litro, Garrafa, Tambor, Caja
    public decimal FactorConversion { get; set; }       // Litros por unidad
    public string? CodigoBarra { get; set; }
    public decimal PrecioUnitario { get; set; }
}

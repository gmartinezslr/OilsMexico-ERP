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

    // ---------------------------------------------------------------------------
    // Tabulador de comisiones por presentación (fase 3).
    // Vive AQUÍ y no en una tabla cat_presentaciones aparte para no duplicar el factor
    // de conversión: cada producto ya declara sus presentaciones con sus litros, así que
    // un SKU nuevo hereda el % en el mismo alta (no requiere código ni segunda captura).
    // Ambos son % (5 = 5%), NO fracciones.
    // ---------------------------------------------------------------------------

    /// <summary>% de comisión si NO se cumple la meta del mes (0 = esta presentación no comisiona).</summary>
    public decimal PorcComisionBase { get; set; }

    /// <summary>% de comisión si SÍ se cumple la meta. Debe ser >= PorcComisionBase.</summary>
    public decimal PorcComisionBono { get; set; }
}

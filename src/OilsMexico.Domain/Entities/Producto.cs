namespace OilsMexico.Domain.Entities;

/// <summary>Catálogo maestro de lubricantes. Tabla: productos.</summary>
public sealed class Producto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Viscosidad { get; set; } = string.Empty;   // Ej: 5W-30
    public string TipoBase { get; set; } = string.Empty;     // Sintetico, Semisintetico, Mineral
    public string? DescripcionTecnica { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal PrecioMayoreo { get; set; }
    public decimal PrecioCosto { get; set; }
    public bool Activo { get; set; } = true;
    public List<UnidadMedida> Unidades { get; set; } = [];
}

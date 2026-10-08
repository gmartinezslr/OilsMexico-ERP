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
    public string? Categoria { get; set; }                   // Ej: SINTETICOS, SEMI SINTETICOS, MINERALES, MOTO, TRANSMISIONES, HD
    public string? SkuAnterior { get; set; }                // SKU anterior / alterno de lista de precios
    public string? Sae { get; set; }                        // Grado SAE (ej: 0W-20, 5W-30, 40)
    public string? Especificacion { get; set; }             // Norma SAT / fabricante (ej: API SP, JASO MA, Mercon V)
    public int PiezasPorCaja { get; set; } = 1;             // Unidades por caja / empaque maestro
    public decimal PrecioLista { get; set; }                // Precio LP (Precio de lista de la presentación/caja)
    public decimal PrecioLpOroConIva { get; set; }          // LP Oro Precio con IVA (precio distribuidor con IVA)
    public decimal PrecioUnitario { get; set; }             // Precio unitario por pieza con IVA
    public decimal PrecioVenta { get; set; }
    public decimal PrecioMayoreo { get; set; }
    public decimal PrecioCosto { get; set; }
    public bool Activo { get; set; } = true;
    public List<UnidadMedida> Unidades { get; set; } = [];
}

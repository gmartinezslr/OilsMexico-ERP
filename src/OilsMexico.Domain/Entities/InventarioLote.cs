namespace OilsMexico.Domain.Entities;

/// <summary>Trazabilidad por lote y caducidad. Tabla: inventario_lotes.</summary>
public sealed class InventarioLote
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public string NumeroLote { get; set; } = string.Empty;
    public DateOnly? FechaFabricacion { get; set; }
    public DateOnly? FechaCaducidad { get; set; }
    public decimal CantidadDisponible { get; set; }
    public int? AlmacenId { get; set; }
}

namespace OilsMexico.Domain.Entities;

/// <summary>Stock en tiempo real por sucursal + lote. Tabla: inventario_sucursal.</summary>
public sealed class InventarioSucursal
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int? LoteId { get; set; }
    public InventarioLote? Lote { get; set; }
    public decimal StockActual { get; set; }
    public decimal StockMinimo { get; set; } = 5m;
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}

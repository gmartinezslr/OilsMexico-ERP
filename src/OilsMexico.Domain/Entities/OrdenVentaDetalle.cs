namespace OilsMexico.Domain.Entities;

/// <summary>Renglón de orden de venta.</summary>
public sealed class OrdenVentaDetalle
{
    public int Id { get; set; }
    public int OrdenVentaId { get; set; }
    public OrdenVenta? OrdenVenta { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int UnidadMedidaId { get; set; }
    public string UnidadNombre { get; set; } = "Litro";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }    // precio confirmado de la cotización
    public decimal Descuento { get; set; }
    public decimal Importe { get; set; }
    public int? LoteId { get; set; }
}

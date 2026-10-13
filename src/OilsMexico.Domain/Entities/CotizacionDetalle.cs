namespace OilsMexico.Domain.Entities;

/// <summary>Renglón de cotización.</summary>
public sealed class CotizacionDetalle
{
    public int Id { get; set; }
    public int CotizacionId { get; set; }
    public Cotizacion? Cotizacion { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int UnidadMedidaId { get; set; }
    public string UnidadNombre { get; set; } = "Litro";
    public decimal Cantidad { get; set; }          // en unidades de venta
    public decimal PrecioUnitario { get; set; }    // precio de compromiso
    public decimal Descuento { get; set; }         // en pesos (se descuenta del subtotal)
    public decimal Importe { get; set; }           // calculado: cantidad * precio - descuento
    public int? LoteId { get; set; }
}

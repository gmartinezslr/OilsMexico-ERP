using OilsMexico.Domain.Enums;

namespace OilsMexico.Domain.Entities;

/// <summary>Encabezado de orden de venta (derivada de cotización o manual).</summary>
public sealed class OrdenVenta
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? VendedorId { get; set; }
    public Usuario? Vendedor { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public string? FolioInterno { get; set; }
    public string? Notas { get; set; }
    public EstadoOrdenVenta Estado { get; set; } = EstadoOrdenVenta.Borrador;
    /// <summary>Cotización de origen: null = orden manual; con valor deriva de la cotización.</summary>
    public int? CotizacionId { get; set; }
    public Cotizacion? Cotizacion { get; set; }
    /// <summary>
    /// Factura generada al convertir la orden en venta (Capa B del pipeline comercial).
    /// null = la orden aún no se ha facturado. Sirve de guardia contra doble facturación y
    /// para navegar de la orden al CFDI. Se fija en CotizacionesService.ConvertirOrdenAVentaAsync.
    /// </summary>
    public int? FacturaId { get; set; }
    public Factura? Factura { get; set; }
    public int UsuarioId { get; set; }
    public List<OrdenVentaDetalle> Detalles { get; set; } = [];

    /// <summary>Totales desnormalizados (se recalculan al crear/convertir desde los renglones).</summary>
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
}

namespace OilsMexico.Domain.Entities;

using OilsMexico.Domain.Enums;

/// <summary>
/// Nota de Crédito CFDI 4.0 (TipoComprobante=E Egreso).
/// Documento fiscal electrónico separado de la devolución interna del Historial:
/// ampara devoluciones / descuentos / bonificaciones al cliente con UUID propio
/// y CfdiRelacionados TipoRelación 01 al UUID de la factura origen.
/// Tabla: notas_credito.
/// </summary>
public sealed class NotaCredito
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    /// <summary>Folio interno serie NC: NC-{suc}-{yyyyMMddHHmmss}.</summary>
    public string FolioInterno { get; set; } = string.Empty;
    public int FacturaOrigenId { get; set; }
    public Factura? FacturaOrigen { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    /// <summary>UUID de la factura origen (CfdiRelacionados 01).</summary>
    public Guid? UuidFacturaOrigen { get; set; }
    public Guid? UuidSat { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    /// <summary>Devolucion | Descuento | Bonificacion.</summary>
    public string Motivo { get; set; } = "Devolucion";
    public string UsoCfdi { get; set; } = "G02";
    public string TipoRelacion { get; set; } = "01";
    public string? XmlSellado { get; set; }
    public string? SelloDigital { get; set; }
    public string? CadenaOriginal { get; set; }
    /// <summary>Pendiente | Timbrada | Cancelada.</summary>
    public EstadoNotaCredito Estado { get; set; } = EstadoNotaCredito.Pendiente;
    public string? MotivoCancelacion { get; set; }
    public bool RepusoStock { get; set; }
    public string? Observaciones { get; set; }
    public int UsuarioId { get; set; }
    public List<NotaCreditoDetalle> Detalles { get; set; } = [];
}

/// <summary>Renglón de la NC (copia de la factura origen).</summary>
public sealed class NotaCreditoDetalle
{
    public int Id { get; set; }
    public int NotaCreditoId { get; set; }
    public NotaCredito? NotaCredito { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public string UnidadNombre { get; set; } = "Litro";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
    public int? LoteId { get; set; }
}

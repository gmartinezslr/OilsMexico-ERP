using OilsMexico.Domain.Enums;

namespace OilsMexico.Domain.Entities;

/// <summary>Encabezado de venta / CFDI 4.0. Tabla: facturas.</summary>
public sealed class Factura
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public string FolioInterno { get; set; } = string.Empty;
    public Guid? UuidSat { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public string? XmlSellado { get; set; }
    public string? SelloDigital { get; set; }
    public string? CadenaOriginal { get; set; }
    public string MetodoPagoSat { get; set; } = "PUE"; // PUE | PPD
    public string FormaPagoSat { get; set; } = "01";    // Catálogo SAT c_FormaPago
    public string UsoCfdi { get; set; } = "G03";
    public EstadoFactura Estado { get; set; } = EstadoFactura.Pendiente;
    public List<FacturaDetalle> Detalles { get; set; } = [];
}

/// <summary>Renglón de factura.</summary>
public sealed class FacturaDetalle
{
    public int Id { get; set; }
    public int FacturaId { get; set; }
    public Factura? Factura { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int? UnidadMedidaId { get; set; }
    public string UnidadNombre { get; set; } = "Litro";
    public decimal Cantidad { get; set; }          // en unidades de venta
    public decimal LitrosDescontados { get; set; } // cantidad * factor
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
    public int? LoteId { get; set; }
}

namespace OilsMexico.Domain.Entities;

/// <summary>
/// Cobro / parcialidad contra una venta PPD (cuentas por cobrar).
/// Nace vinculado a un ComplementoPago (REP); el REP agrupa 1..N cobros.
/// Tabla: venta_cobros.
/// </summary>
public sealed class VentaCobro
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int FacturaId { get; set; }
    public Factura? Factura { get; set; }
    public int ClienteId { get; set; }
    public decimal Monto { get; set; }
    public string FormaPagoSat { get; set; } = "01";
    public DateTime FechaPagoUtc { get; set; } = DateTime.UtcNow;
    public string? Referencia { get; set; }
    public int UsuarioId { get; set; }
    public int? ComplementoPagoId { get; set; }
    public ComplementoPago? ComplementoPago { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Complemento de pagos / REP (CFDI TipoComprobante=P, Uso CP01).
/// Agrupa 1..N cobros del mismo receptor y se timbra con UUID propio.
/// Tabla: complementos_pago.
/// </summary>
public sealed class ComplementoPago
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    /// <summary>Folio interno serie REP: REP-{suc}-{yyyyMMddHHmmss}.</summary>
    public string FolioInterno { get; set; } = string.Empty;
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public Guid? UuidSat { get; set; }
    public string? XmlSellado { get; set; }
    public string? SelloDigital { get; set; }
    public string? CadenaOriginal { get; set; }
    /// <summary>Pendiente | Timbrado | Cancelado.</summary>
    public string Estado { get; set; } = "Pendiente";
    public int UsuarioId { get; set; }
    public List<ComplementoPagoDetalle> Documentos { get; set; } = [];
}

/// <summary>DoctoRelacionado dentro del REP (parcialidad de una factura PPD).</summary>
public sealed class ComplementoPagoDetalle
{
    public int Id { get; set; }
    public int ComplementoPagoId { get; set; }
    public ComplementoPago? ComplementoPago { get; set; }
    public int FacturaId { get; set; }
    public Factura? Factura { get; set; }
    public int VentaCobroId { get; set; }
    public Guid? UuidFactura { get; set; }
    public string FolioFactura { get; set; } = string.Empty;
    public int NumParcialidad { get; set; } = 1;
    public decimal ImpSaldoAnt { get; set; }
    public decimal ImpPagado { get; set; }
    public decimal ImpSaldoInsoluto { get; set; }
    public string Moneda { get; set; } = "MXN";
}

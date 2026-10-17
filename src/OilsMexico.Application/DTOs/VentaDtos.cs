using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

public sealed record ProductoDto(
    int Id, string Sku, string Nombre, string Marca, string Viscosidad,
    string TipoBase, decimal PrecioVenta, decimal PrecioMayoreo,
    decimal StockSucursal, string UnidadVenta, decimal FactorConversion,
    int UnidadMedidaId = 0, string? CodigoBarra = null);

public sealed record CarritoItemDto(
    int ProductoId, string Sku, string Nombre,
    int UnidadMedidaId, string UnidadNombre, decimal FactorConversion,
    decimal Cantidad, decimal PrecioUnitario)
{
    public decimal Litros => Cantidad * FactorConversion;
    public decimal Importe => Cantidad * PrecioUnitario;
}

/// <summary>
/// Solicitud de venta POS. <c>CongelarPrecios = true</c> respeta exactamente el precio de cada
/// renglón (no aplica el override de precio mayoreo del cliente): lo usa el pipeline
/// Cotización → Orden → Factura para conservar el precio de compromiso.
/// </summary>
public sealed record VentaPosRequest(
    int SucursalId, int ClienteId, int UsuarioId,
    int? VendedorId,
    string FormaPagoSat, string MetodoPagoSat, string UsoCfdi,
    bool RequiereFactura, List<CarritoItemDto> Items,
    bool CongelarPrecios = false);

public sealed record VentaPosResult(
    int FacturaId, string FolioInterno, Guid? UuidSat,
    decimal Subtotal, decimal Iva, decimal Total, EstadoFactura Estado);

public sealed record CatalogoSatItemDto(
    string Clave,
    string Descripcion,
    string? Grupo = null,
    int Orden = 0);

public sealed record CatalogosSatDto(
    Dictionary<string, string> FormasPago,
    Dictionary<string, string> MetodosPago,
    Dictionary<string, string> UsosCfdi,
    Dictionary<string, string> Regimenes,
    Dictionary<string, string>? MotivosCancelacion = null,
    Dictionary<string, string>? SituacionesFiscales = null,
    Dictionary<string, string>? GruposFormasPago = null)
{
    public Dictionary<string, string> MotivosCancelacion { get; init; } = MotivosCancelacion ?? [];
    public Dictionary<string, string> SituacionesFiscales { get; init; } = SituacionesFiscales ?? [];
    public Dictionary<string, string> GruposFormasPago { get; init; } = GruposFormasPago ?? [];
}

public sealed record HistorialVentaDto(
    int FacturaId, string FolioInterno, DateTime FechaEmision,
    string Cliente, EstadoFactura Estado, decimal Subtotal, decimal Iva, decimal Total,
    Guid? UuidSat, int Renglones);

public sealed record HistorialResumenDto(int Ventas, decimal Subtotal, decimal Iva, decimal Total);

public sealed record HistorialResultadoDto(
    List<HistorialVentaDto> Ventas, HistorialResumenDto Resumen,
    int TotalRegistros, int Pagina, int Paginas);

public sealed record HistorialLineaDto(
    string Producto, decimal Cantidad, string Unidad, decimal PrecioUnitario, decimal Importe);

public sealed record HistorialDetalleDto(
    int FacturaId, string FolioInterno, DateTime FechaEmision, string Cliente, EstadoFactura Estado,
    string FormaPagoSat, string MetodoPagoSat, string UsoCfdi,
    decimal Subtotal, decimal Iva, decimal Total, Guid? UuidSat,
    string? SelloDigital, string? CadenaOriginal, List<HistorialLineaDto> Lineas);

/// <summary>Renglón del módulo Facturación CFDI (listado de folios con estado fiscal).</summary>
public sealed record FacturaListadoDto(
    int FacturaId, string FolioInterno, DateTime FechaEmision, string Cliente,
    EstadoFactura Estado, decimal Subtotal, decimal Iva, decimal Total,
    string FormaPagoSat, string MetodoPagoSat, string UsoCfdi,
    Guid? UuidSat, bool TieneXml, int Renglones, string? MotivoCancelacion);

/// <summary>Estado de crédito de un cliente para el bloqueo por morosidad (§4.1).</summary>
public sealed record CreditoClienteDto(
    int ClienteId, string Nombre, decimal LimiteCredito, int DiasCredito,
    decimal SaldoVencido, bool EstaBloqueado)
{
    public bool TieneCredito => LimiteCredito > 0;

    /// <summary>Texto legible para mostrar en la UI del POS.</summary>
    public string EstadoTexto => EstaBloqueado
        ? $"Bloqueado: vencido {SaldoVencido:N2} supera límite {LimiteCredito:N2}"
        : TieneCredito
            ? $"Disponible: {LimiteCredito - SaldoVencido:N2} / {LimiteCredito:N2}"
            : "Contado (sin crédito)";
}

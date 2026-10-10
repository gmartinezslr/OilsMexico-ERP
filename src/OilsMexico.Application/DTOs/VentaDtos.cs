using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

public sealed record ProductoDto(
    int Id, string Sku, string Nombre, string Marca, string Viscosidad,
    string TipoBase, decimal PrecioVenta, decimal PrecioMayoreo,
    decimal StockSucursal, string UnidadVenta, decimal FactorConversion);

public sealed record CarritoItemDto(
    int ProductoId, string Sku, string Nombre,
    int UnidadMedidaId, string UnidadNombre, decimal FactorConversion,
    decimal Cantidad, decimal PrecioUnitario)
{
    public decimal Litros => Cantidad * FactorConversion;
    public decimal Importe => Cantidad * PrecioUnitario;
}

public sealed record VentaPosRequest(
    int SucursalId, int ClienteId, int UsuarioId,
    int? VendedorId,
    string FormaPagoSat, string MetodoPagoSat, string UsoCfdi,
    bool RequiereFactura, List<CarritoItemDto> Items);

public sealed record VentaPosResult(
    int FacturaId, string FolioInterno, Guid? UuidSat,
    decimal Subtotal, decimal Iva, decimal Total, string Estado);

public sealed record CatalogosSatDto(
    Dictionary<string, string> FormasPago,
    Dictionary<string, string> MetodosPago,
    Dictionary<string, string> UsosCfdi,
    Dictionary<string, string> Regimenes);

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

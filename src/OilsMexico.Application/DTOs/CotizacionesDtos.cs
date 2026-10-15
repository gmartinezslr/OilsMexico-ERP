using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

/// <summary>Fila de la lista de cotizaciones.</summary>
public sealed record CotizacionListadoDto(
    int Id, string FolioInterno, DateTime FechaEmision, string Cliente,
    string? Vendedor, EstadoCotizacion Estado, int Renglones,
    decimal Subtotal, decimal Iva, decimal Total);

/// <summary>Renglón de cotización (detalle).</summary>
public sealed record CotizacionRenglonDto(
    int Id, int ProductoId, string Sku, string Nombre, int UnidadMedidaId,
    string UnidadNombre, decimal Cantidad, decimal PrecioUnitario, decimal Descuento, decimal Importe);

/// <summary>Renglón crudo usado al crear/editar una cotización.</summary>
public sealed record CotizacionRenglonCrearDto(
    int ProductoId, int? UnidadMedidaId, decimal Cantidad, decimal PrecioUnitario, decimal Descuento);

/// <summary>Solicitud de creación/actualización de cotización.</summary>
public sealed record CotizacionCrearRequest(
    int ClienteId, int? VendedorId, DateTime? FechaVencimiento, string? Notas, List<CotizacionRenglonCrearDto> Detalles);

/// <summary>Resultado de la alta de cotización.</summary>
public sealed record CotizacionCrearResult(int Id, string FolioInterno);

/// <summary>Solicitud de conversión de cotización en orden de venta.</summary>
public sealed record CotizacionConvertirRequest(
    int? VendedorId, string? FolioInterno, string? Motivo);

/// <summary>Resultado de la conversión: nueva orden de venta creada.</summary>
public sealed record CotizacionConvertirResult(
    int OrdenVentaId, string FolioInterno, decimal Total, EstadoOrdenVenta EstadoNuevaOrden);

/// <summary>Encabezado completo de cotización (solo lectura) para pantalla de detalle.</summary>
public sealed record CotizacionResponseDto(
    int Id, int SucursalId, string Sucursal, int ClienteId, string Cliente,
    int? VendedorId, string? Vendedor, DateTime FechaEmision, DateTime? FechaVencimiento,
    string? FolioInterno, string? Notas, EstadoCotizacion Estado, List<CotizacionRenglonDto> Detalles,
    decimal Subtotal, decimal Iva, decimal Total, string? ClienteEmail = null);

/// <summary>Fila de la lista de órdenes de venta.</summary>
public sealed record OrdenVentaListadoDto(
    int Id, string FolioInterno, DateTime FechaEmision, string Cliente,
    string? Vendedor, EstadoOrdenVenta Estado, int Renglones,
    decimal Subtotal, decimal Iva, decimal Total, int? CotizacionId);

/// <summary>Renglón de orden de venta.</summary>
public sealed record OrdenVentaDetalleLineaDto(
    int Id, int ProductoId, string Sku, string Nombre, int UnidadMedidaId,
    string UnidadNombre, decimal Cantidad, decimal PrecioUnitario, decimal Descuento, decimal Importe);

/// <summary>Encabezado completo de orden de venta para pantalla de detalle.</summary>
public sealed record OrdenVentaDetalleDto(
    int Id, int SucursalId, string Sucursal, int ClienteId, string Cliente,
    int? VendedorId, string? Vendedor, DateTime FechaEmision,
    string? FolioInterno, string? Notas, EstadoOrdenVenta Estado,
    List<OrdenVentaDetalleLineaDto> Detalles, decimal Subtotal, decimal Iva, decimal Total,
    int CotizacionId, int? FacturaId);

/// <summary>Solicitud de creación de orden de venta manual.</summary>
public sealed record OrdenVentaCrearRequest(
    int ClienteId, int? VendedorId, int? CotizacionId, string? FolioInterno, string? Notas,
    List<OrdenVentaDetalleLineaCrearDto> Detalles);

/// <summary>Renglón crudo usado al crear una orden de venta manual.</summary>
public sealed record OrdenVentaDetalleLineaCrearDto(
    int ProductoId, int? UnidadMedidaId, decimal Cantidad, decimal PrecioUnitario, decimal Descuento);

/// <summary>Resultado de la alta de orden de venta.</summary>
public sealed record OrdenVentaCrearResult(int Id, string FolioInterno);

/// <summary>Solicitud de confirmación de orden de venta (precio y vendedor).</summary>
public sealed record OrdenVentaConfirmarRequest(int? VendedorId, string? Motivo);

/// <summary>Resultado de la confirmación de orden de venta.</summary>
public sealed record OrdenVentaConfirmarResult(int Id, string FolioInterno, EstadoOrdenVenta Estado);

/// <summary>
/// Facturación de una orden de venta confirmada (Capa B): se genera la venta/CFDI con los
/// precios de compromiso congelados en la orden. Los códigos SAT por defecto se toman de
/// <c>CfdiDefaults</c> cuando vienen vacíos. <see cref="RequiereFactura"/> = false deja la venta
/// pendiente de timbrar (útil para pruebas o para diferir el timbrado).
/// </summary>
public sealed record OrdenVentaFacturarRequest(
    string FormaPagoSat, string MetodoPagoSat, string UsoCfdi, bool RequiereFactura);

/// <summary>Resultado de facturar la orden: la orden pasa a Facturada y queda ligada a la Factura.</summary>
public sealed record OrdenVentaFacturarResult(
    int OrdenVentaId, string FolioOrden, EstadoOrdenVenta EstadoOrden,
    int FacturaId, string FolioFactura, EstadoFactura EstadoFactura,
    decimal Subtotal, decimal Iva, decimal Total, Guid? UuidSat);

/// <summary>Resultado de cancelación de cotización u orden de venta.</summary>
public sealed record OrdenResultado(int Id, string FolioInterno, string Estado, string Tipo);

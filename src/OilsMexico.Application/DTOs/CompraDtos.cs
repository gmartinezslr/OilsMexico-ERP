namespace OilsMexico.Application.DTOs;

/// <summary>Renglón del listado de compras (orden + recepción + CxP).</summary>
public sealed record CompraListadoDto(
    int CompraId, string FolioInterno, DateTime FechaEmision, string Proveedor,
    string? FolioProveedor, string Estado, string EstadoPago,
    decimal Subtotal, decimal Iva, decimal Total, decimal MontoPagado,
    int Renglones, decimal Saldo)
{
    public decimal PorcentajeRecibido { get; init; }
}

/// <summary>Renglón de una orden de compra (crear).</summary>
public sealed record CompraRenglonDto(
    int ProductoId, int? UnidadMedidaId, string UnidadNombre, decimal FactorConversion,
    decimal Cantidad, decimal CostoUnitario)
{
    public decimal Importe => Cantidad * CostoUnitario;
    public decimal Litros => Cantidad * FactorConversion;
}

public sealed record CrearCompraRequest(
    int SucursalId, int ProveedorId, string? FolioProveedor, string? Notas,
    List<CompraRenglonDto> Renglones);

public sealed record CompraResult(
    int CompraId, string FolioInterno, decimal Subtotal, decimal Iva, decimal Total, string Estado);

/// <summary>Renglón a recibir (cantidades parciales por renglón).</summary>
public sealed record RecepcionRenglonDto(
    int DetalleId, decimal CantidadRecibir, string? NumeroLote, DateOnly? FechaCaducidad);

public sealed record RecepcionRequest(
    int CompraId, List<RecepcionRenglonDto> Renglones);

public sealed record PagoCompraRequest(
    int CompraId, decimal Monto, string FormaPago, string? Referencia);

/// <summary>Detalle completo de una compra (renglones + recepciones + pagos).</summary>
public sealed record CompraDetalleLineaDto(
    int DetalleId, string Producto, string Sku, string UnidadNombre,
    decimal Cantidad, decimal CantidadRecibida, decimal CostoUnitario, decimal Importe,
    string? NumeroLote, DateOnly? FechaCaducidad);

public sealed record CompraPagoDto(
    int PagoId, DateTime Fecha, decimal Monto, string FormaPago, string? Referencia, string Usuario);

public sealed record CompraDetalleDto(
    int CompraId, string FolioInterno, DateTime FechaEmision, string Proveedor,
    string? FolioProveedor, string Estado, string EstadoPago,
    decimal Subtotal, decimal Iva, decimal Total, decimal MontoPagado, decimal Saldo,
    string? Notas, List<CompraDetalleLineaDto> Lineas, List<CompraPagoDto> Pagos);

/// <summary>Cuentas por pagar: compras con saldo pendiente.</summary>
public sealed record CuentasPorPagarDto(
    int CompraId, string FolioInterno, DateTime FechaEmision, string Proveedor,
    string? FolioProveedor, string Estado, decimal Total, decimal MontoPagado, decimal Saldo);

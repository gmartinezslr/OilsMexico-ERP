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

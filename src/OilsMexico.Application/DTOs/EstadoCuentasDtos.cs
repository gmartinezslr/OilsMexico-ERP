namespace OilsMexico.Application.DTOs;

/// <summary>Registro de estado de cuenta de un cliente.</summary>
public sealed record EstadoCuentaClienteDto(
    int ClienteId, string Nombre, string RFC, string Telefono, string Email,
    decimal TotalFacturasAbiertas, decimal TotalPagosAplicados, decimal SaldoPendiente);

/// <summary>Un documento (factura o NC) abiertos para cobrar.</summary>
public sealed record FacturaEstadoCuentaDto(
    int Id, string FolioInterno, DateTime FechaEmision, string Estado, decimal Total, decimal? UuidSat);

/// <summary>Un pago/deducción aplicado contra el cliente.</summary>
public sealed record PagoCuentaDto(
    int Id, DateTime Fecha, string Tipo, decimal Monto, string Referencia, string? Porcentaje);

/// <summary>Detalle de estado de cuenta de un cliente.</summary>
public sealed record ClienteEstadoCuentaDto(
    int Id, string Nombre, string RFC, string Telefono, string Email,
    List<FacturaEstadoCuentaDto> FacturasAbiertas, List<PagoCuentaDto> Pagos, decimal SaldoPendiente);

/// <summary>Resultado de búsqueda de estado de cuenta.</summary>
public sealed record BusquedaEstadoCuentaResultado(
    List<EstadoCuentaClienteDto> Clientes, int TotalRegistros, int Pagina, int Paginas);

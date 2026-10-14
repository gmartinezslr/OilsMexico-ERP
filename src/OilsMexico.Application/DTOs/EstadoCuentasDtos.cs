using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

/// <summary>Registro de estado de cuenta de un cliente.</summary>
public sealed record EstadoCuentaClienteDto(
    int ClienteId, string Nombre, string RFC, string Telefono, string Email,
    decimal TotalFacturasAbiertas, decimal TotalPagosAplicados, decimal SaldoPendiente);

/// <summary>Un documento (factura o NC) abiertos para cobrar.</summary>
public sealed record FacturaEstadoCuentaDto(
    int Id, string FolioInterno, DateTime FechaEmision, EstadoFactura Estado, decimal Total, Guid? UuidSat);

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

// ─────────────────────────────────────────────────────────────────────────────
// §1.2 Antigüedad de saldos CxC (EstadoCuentasService.Antiguedad.cs)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Factura vencida con días de atraso e interés moratorio calculado.
/// DiasVencida = días transcurridos desde FechaVencimiento hasta hoy.
/// </summary>
public sealed record FacturaVencidaDto(
    int FacturaId, string FolioInterno, DateTime FechaEmision, DateTime FechaVencimiento,
    int DiasVencida, decimal SaldoPendiente, decimal InteresCalculado);

/// <summary>
/// Antigüedad de saldos CxC de un cliente: saldos agrupados en rangos
/// 0-15 / 16-30 / 31-60 / +60 días con totales e intereses.
/// </summary>
public sealed record AntiguedadClienteDto(
    int ClienteId, string Nombre, string RFC, string Telefono, string Email,
    decimal Rango0_15,   // saldo con 0-15 días
    decimal Rango16_30,  // saldo con 16-30 días
    decimal Rango31_60,  // saldo con 31-60 días
    decimal Rango60Mas,  // saldo con más de 60 días
    decimal TotalSaldo,
    decimal TotalInteres,
    List<FacturaVencidaDto> Facturas);

/// <summary>Resumen de antigüedad de saldos CxC para toda la sucursal.</summary>
public sealed record AntiguedadSaldosCxcDto(
    decimal TotalRango0_15, decimal TotalRango16_30,
    decimal TotalRango31_60, decimal TotalRango60Mas,
    decimal TotalSaldo, decimal TotalInteres,
    List<AntiguedadClienteDto> Clientes);

// ─────────────────────────────────────────────────────────────────────────────
// §1.2 Antigüedad de saldos CxP (ComprasService.Antiguedad.cs)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Compra vencida con días de atraso e interés moratorio calculado.
/// </summary>
public sealed record CompraVencidaDto(
    int CompraId, string FolioInterno, DateTime FechaEmision, DateTime FechaVencimiento,
    string Proveedor, int DiasVencida, decimal Saldo, decimal InteresCalculado);

/// <summary>Antigüedad de saldos CxP agrupados por proveedor.</summary>
public sealed record AntiguedadProveedorDto(
    int ProveedorId, string Proveedor,
    decimal Rango0_15, decimal Rango16_30, decimal Rango31_60, decimal Rango60Mas,
    decimal TotalSaldo, decimal TotalInteres,
    List<CompraVencidaDto> Compras);

/// <summary>Resumen de antigüedad de saldos CxP para toda la sucursal.</summary>
public sealed record AntiguedadSaldosCxpDto(
    decimal TotalRango0_15, decimal TotalRango16_30,
    decimal TotalRango31_60, decimal TotalRango60Mas,
    decimal TotalSaldo, decimal TotalInteres,
    List<AntiguedadProveedorDto> Proveedores);


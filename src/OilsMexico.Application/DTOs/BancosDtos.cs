using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

// ---------------------------------------------------------------------------
// Módulo de conciliación bancaria. Las cuentas guardan el extracto; los
// movimientos del extracto se empatan con VentaCobro (Abono) y CompraPago (Cargo).
// ---------------------------------------------------------------------------

/// <summary>Renglón del listado de cuentas bancarias de una sucursal.</summary>
public sealed record CuentaBancariaDto(
    int Id, int SucursalId, string Banco, string Alias,
    string? NumeroCuenta, string? Clabe, string Moneda,
    decimal SaldoInicial, bool Activa,
    int Pendientes, int Conciliados, decimal SaldoCalculado);

/// <summary>Alta/edición de una cuenta bancaria.</summary>
public sealed record CrearCuentaBancariaRequest(
    int SucursalId, string Banco, string Alias,
    string? NumeroCuenta, string? Clabe, string Moneda, decimal SaldoInicial);

/// <summary>Renglón del extracto bancario ya capturado en el ERP.</summary>
public sealed record MovimientoBancarioDto(
    int Id, int CuentaBancariaId, DateTime Fecha, string Concepto, string? Referencia,
    NaturalezaMovimiento Naturaleza, decimal Monto, EstadoConciliacion Estado,
    int? VentaCobroId, int? CompraPagoId, string? ConciliadoCon, DateTime? ConciliadoUtc);

/// <summary>Alta de un renglón del extracto (captura manual o importada).</summary>
public sealed record CargarMovimientoRequest(
    int CuentaBancariaId, DateTime Fecha, string Concepto, string? Referencia,
    NaturalezaMovimiento Naturaleza, decimal Monto);

/// <summary>
/// Movimiento del ERP (un cobro o un pago) todavía SIN conciliar: candidato a
/// empatar con un renglón del extracto. <see cref="Origen"/> distingue «Cobro»/«Pago».
/// </summary>
public sealed record MovimientoErpDto(
    string Origen, int Id, DateTime Fecha, string Contraparte,
    string? Referencia, decimal Monto);

/// <summary>
/// Empata un renglón del extracto con un movimiento del ERP. El servicio valida que la
/// naturaleza coincida (Abono→Cobro, Cargo→Pago) y que ninguno esté ya conciliado.
/// </summary>
public sealed record ConciliarMovimientoRequest(
    int MovimientoId, string Origen, int MovimientoErpId);

/// <summary>Resumen de conciliación de una cuenta: saldos y pendientes.</summary>
public sealed record ResumenConciliacionDto(
    int CuentaBancariaId, string Banco, string Alias, string Moneda,
    decimal SaldoInicial, decimal TotalAbonos, decimal TotalCargos,
    decimal SaldoCalculado, int Pendientes, int Conciliados);

namespace OilsMexico.Domain.Enums;

public enum EstadoFactura
{
    Pendiente,
    Surtida,
    Timbrada,
    Entregada,
    Cancelada,
    Devolucion
}

public enum TipoMovimiento
{
    Venta,
    Compra,
    Ajuste,
    TraspasoIn,
    TraspasoOut,
    Devolucion
}

// ---------------------------------------------------------------------------
// Módulo de comisiones (fase 3). Se guardan como texto en PG (ver ComisionConfigs).
// ---------------------------------------------------------------------------

/// <summary>
/// Condición de liberación de la comisión de una cuota.
/// SOLO_DINERO / SOLO_VOLUMEN exigen únicamente esa meta (la otra se ignora aunque esté capturada).
/// </summary>
public enum OperadorLogico
{
    SoloDinero,
    SoloVolumen,
    Y,
    O
}

/// <summary>
/// Qué se paga cuando NO se cumple la meta.
/// CeroComision    → no se paga nada.
/// PagoMinimo      → piso garantizado: se paga el mayor entre la comisión base y el monto mínimo.
/// </summary>
public enum AccionIncumplimiento
{
    CeroComision,
    PagoMinimo
}

/// <summary>Estado del cierre mensual congelado.</summary>
public enum EstadoComision
{
    Borrador,
    Cerrado,
    Pagado
}

// ---------------------------------------------------------------------------
// Módulo de compras (orden → recepción → CxP). Se guardan como texto en PG
// (ver CompraConfigs: HasConversion<string>).
// ---------------------------------------------------------------------------

/// <summary>Estado de la orden de compra: Borrador → Parcial/Recibida | Cancelada.</summary>
public enum EstadoCompra
{
    Borrador,
    Parcial,
    Recibida,
    Cancelada
}

/// <summary>Estado de pago de la compra (cuentas por pagar).</summary>
public enum EstadoPagoCompra
{
    Pendiente,
    Parcial,
    Pagada
}

// ---------------------------------------------------------------------------
// Notas de crédito CFDI (Tipo E). Se guarda como texto en PG (ver FiscalConfigs).
// ---------------------------------------------------------------------------

/// <summary>Estado de la NC electrónica: Pendiente (sin timbrar) → Timbrada | Cancelada.</summary>
public enum EstadoNotaCredito
{
    Pendiente,
    Timbrada,
    Cancelada
}

// ---------------------------------------------------------------------------
// Complemento de pagos / REP (CFDI Tipo P). Se guarda como texto en PG
// (ver RepConfigs: HasConversion<string>); la columna ya existía como varchar.
// ---------------------------------------------------------------------------

/// <summary>
/// Estado del REP: Pendiente (sin timbrar) → Timbrado | Cancelado.
/// Sustituye el literal <c>"Pendiente"</c> que vivía en <c>ComplementoPago.Estado</c>
/// (checklist §6.2 A9 — estados string/enum heterogéneos).
/// </summary>
public enum EstadoRep
{
    Pendiente,
    Timbrado,
    Cancelado
}

// ---------------------------------------------------------------------------
// Corte de caja. Se guarda como texto en PG (ver CajaConfigs: HasConversion<string>);
// los valores existentes en la tabla ("Abierto"/"Cerrado") coinciden con los nombres.
// ---------------------------------------------------------------------------

/// <summary>Estado del corte de caja: un solo corte Abierto por sucursal → Cerrado.</summary>
public enum EstadoCorte
{
    Abierto,
    Cerrado
}

// ---------------------------------------------------------------------------
// Módulo de conciliación bancaria. Se guardan como texto en PG (ver BancosPatch),
// con CHECK derivado de Enum.GetNames para no duplicar los valores.
// ---------------------------------------------------------------------------

/// <summary>
/// Sentido del dinero en un renglón del extracto bancario.
/// Abono = entró dinero a la cuenta (se concilia contra un <see cref="Entities.VentaCobro"/>).
/// Cargo  = salió dinero de la cuenta (se concilia contra un <see cref="Entities.CompraPago"/>).
/// </summary>
public enum NaturalezaMovimiento
{
    Abono,
    Cargo
}

/// <summary>Estado de la conciliación de un movimiento bancario.</summary>
public enum EstadoConciliacion
{
    Pendiente,
    Conciliado
}

// ---------------------------------------------------------------------------
// Módulo de gastos operativos (/gastos). Se guardan como texto en PG
// (ver GastosPatch), con CHECK derivado de Enum.GetNames para no duplicar
// los valores (mismo patrón que BancosPatch/ComisionPatch).
// ---------------------------------------------------------------------------

/// <summary>Categoría operativa del gasto (clasificación para el reporte por período).</summary>
public enum CategoriaGasto
{
    Combustible,
    Taller,
    Herramientas,
    Servicios,
    Renta,
    Nomina,
    Papeleria,
    Impuestos,
    Otros
}


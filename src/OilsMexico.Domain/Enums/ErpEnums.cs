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

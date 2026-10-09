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

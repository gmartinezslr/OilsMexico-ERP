namespace OilsMexico.Domain.Enums;

/// <summary>
/// Estados de Situación Fiscal (ESF) definidos por el SAT para personas morales y físicas.
/// Un cliente/proveedor registra su ESF declarada, valorada por el admin, y el RFC se valida
/// en estructura y semántica sobre esa misma fila (no se confía en un RFC "válido" mientras la
/// situación fiscal esté inactiva/cancelada).
/// </summary>
public enum SituacionFiscal
{
    /// <summary>Regular en el SAT; las operaciones de facturación y cobros pueden continuar.</summary>
    Actual = 1,

    /// <summary>Deuda de obligaciones fiscales o de cuentas por cobrar; atención limitada.</summary>
    Adeudada = 2,

    /// <summary>No opera en el periodo; sin emisión de CFDI hasta reactivar la situación.</summary>
    Inactiva = 3,

    /// <summary>Anulada ante el SAT (perjuicio, liquidación insolvency, etc.). No se factura.</summary>
    Cancelada = 4,

    /// <summary>Suspendida temporalmente por incumplimientos; vuelve a ser actual al saldar los puntos.</summary>
    Suspendida = 5,

    /// <summary>Baja definitiva del SAT. El RFC queda válido históricamente pero no se puede nuevamente usar.</summary>
    Baja = 6
}

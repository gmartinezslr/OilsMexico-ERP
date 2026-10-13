namespace OilsMexico.Domain.Enums;

/// <summary>Estado de la cotización.</summary>
public enum EstadoCotizacion
{
    /// <summary>Abierta, aún no comprometida.</summary>
    Borrador,

    /// <summary>Venció sin convertirse en orden de venta.</summary>
    Expirada,

    /// <summary>Se transformó en orden de venta (precio comprometido).</summary>
    Convertida,

    /// <summary>Anulada por el vendedor.</summary>
    Cancelada
}

/// <summary>Estado de la orden de venta.</summary>
public enum EstadoOrdenVenta
{
    /// <summary>Entrada manual o derivada de cotización; aún sin confirmar precio/vendedor.</summary>
    Borrador,

    /// <summary>Precio y vendedor confirmados; lista para facturar.</summary>
    Confirmada,

    /// <summary>Bloqueada temporalmente en revisión.</summary>
    Suspendida,

    /// <summary>Anulada.</summary>
    Cancelada,

    /// <summary>Ya se facturó/vendió: se generó la Factura y se ligó en <see cref="OrdenVenta.FacturaId"/>.</summary>
    Facturada
}

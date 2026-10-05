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

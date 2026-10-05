namespace OilsMexico.Domain.Entities;

/// <summary>Kardex: todo movimiento de almacén queda auditado (quién/cuándo/qué).</summary>
public sealed class MovimientoInventario
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int ProductoId { get; set; }
    public int? LoteId { get; set; }
    public decimal CantidadLitros { get; set; } // + entrada, - salida
    public string Tipo { get; set; } = "VENTA";  // VENTA | COMPRA | AJUSTE | TRASPASO_IN | TRASPASO_OUT | DEVOLUCION
    public string? Motivo { get; set; }
    public int? ReferenciaId { get; set; }       // Id de factura/compra
    public int UsuarioId { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}

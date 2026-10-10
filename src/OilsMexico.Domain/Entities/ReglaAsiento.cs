namespace OilsMexico.Domain.Entities;

/// <summary>Regla de generación de asientos contables. Se mapea a líneas de una póliza: <c>CuentaId</c> y lado (débito/crédito).</summary>
public sealed class ReglaAsiento
{
    public int Id { get; set; }
    public string Evento { get; set; } = string.Empty; // Venta, Compra, Devolucion, etc.
    public int CuentaId { get; set; }
    public CuentaContable? Cuenta { get; set; }
    public bool Debe { get; set; } // true=Débito, false=Crédito
    public string MontoOrigen { get; set; } = string.Empty; // total, subtotal, iva, neto
    public int Orden { get; set; } // orden dentro del evento
    public bool Activo { get; set; } = true;
}

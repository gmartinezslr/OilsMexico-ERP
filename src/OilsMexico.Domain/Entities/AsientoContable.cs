namespace OilsMexico.Domain.Entities;

/// <summary>
/// Póliza contable: un asiento con cabecera + varios detalles (cuentas).
/// </summary>
public sealed class AsientoContable
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int UsuarioId { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Bélico | Operativo | Transferencia | Prepago | Otro.</summary>
    public string Tipo { get; set; } = "Operativo";
    public string? Numeracion { get; set; } // Nº de póliza (asignado por el servicio)
    public string? Concepto { get; set; }
    public string? Notas { get; set; }
    public List<DetalleAsiento> Detalles { get; set; } = [];
}

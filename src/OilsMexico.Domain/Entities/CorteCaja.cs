namespace OilsMexico.Domain.Entities;

/// <summary>
/// Corte / arqueo de caja por turno y sucursal.
/// Un solo corte Abierto por sucursal; al cerrar se cuadra
/// sistema (ventas del turno por forma de pago) vs conteo físico.
/// Tabla: cortes_caja.
/// </summary>
public sealed class CorteCaja
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int UsuarioAperturaId { get; set; }
    public string UsuarioAperturaNombre { get; set; } = string.Empty;
    public int? UsuarioCierreId { get; set; }
    public DateTime FechaAperturaUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierreUtc { get; set; }
    public decimal FondoInicial { get; set; }
    // Foto del sistema al cierre (ventas no canceladas del turno por forma de pago).
    public decimal TotalEfectivoSistema { get; set; }
    public decimal TotalTarjetaSistema { get; set; }
    public decimal TotalTransferSistema { get; set; }
    public decimal TotalOtrosSistema { get; set; }
    public decimal TotalVentasSistema { get; set; }
    public int NumVentas { get; set; }
    // Conteo físico capturado al cierre.
    public decimal EfectivoContado { get; set; }
    /// <summary>Contado - (FondoInicial + EfectivoSistema). 0 = cuadrado.</summary>
    public decimal Diferencia { get; set; }
    /// <summary>Abierto | Cerrado.</summary>
    public string Estado { get; set; } = "Abierto";
    public string? Observaciones { get; set; }
}

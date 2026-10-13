namespace OilsMexico.Domain.Entities;

/// <summary>
/// Gasto operativo de la distribuidora (gasolina, taller, servicios, papelería…).
/// Permite conciliar la caja real: los gastos en efectivo del turno entran al
/// cuadre como egresos (ver CorteCajaService). Tabla: gastos.
/// </summary>
public sealed class Gasto
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    /// <summary>Fecha operativa del gasto (no necesariamente el día de captura).</summary>
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    /// <summary>Categoría operativa. Ver <see cref="Enums.CategoriaGasto"/>.</summary>
    public string Categoria { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public decimal Importe { get; set; }
    /// <summary>Clave SAT c_FormaPago (01 efectivo, 03 transferencia, 04 tarjeta…).</summary>
    public string FormaPagoSat { get; set; } = "01";
    /// <summary>Folio del ticket/factura del proveedor del gasto (comprobante).</summary>
    public string? Comprobante { get; set; }
    /// <summary>Corte de caja al que quedó ligado (solo informativo; el cuadre lee por fecha).</summary>
    public int? CorteCajaId { get; set; }
    public CorteCaja? CorteCaja { get; set; }
    public string? Notas { get; set; }
    public int UsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
}

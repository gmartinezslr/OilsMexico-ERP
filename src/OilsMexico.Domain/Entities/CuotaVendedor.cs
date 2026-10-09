using OilsMexico.Domain.Enums;

namespace OilsMexico.Domain.Entities;

/// <summary>
/// Meta mensual asignada a un vendedor (cuota). Tabla: cuotas_vendedor.
/// Es CONFIGURACIÓN viva: editar una cuota NO altera los cierres ya congelados
/// (el cierre copia los valores que usó en <see cref="ComisionHistorial"/>).
/// </summary>
public sealed class CuotaVendedor
{
    public int Id { get; set; }

    public int VendedorId { get; set; }
    public Usuario? Vendedor { get; set; }

    /// <summary>Año del periodo (se guarda junto al mes en vez de un VARCHAR 'YYYY-MM').</summary>
    public int Anio { get; set; }
    /// <summary>Mes 1-12.</summary>
    public int Mes { get; set; }

    /// <summary>Meta en dinero SIN IVA. NULL = meta no aplica (cuota desactivada).</summary>
    public decimal? MetaDinero { get; set; }

    /// <summary>Meta en litros equivalentes. NULL = meta no aplica.</summary>
    public decimal? MetaLitros { get; set; }

    /// <summary>Y | O | SoloDinero | SoloVolumen — cómo se combinan las metas activas.</summary>
    public OperadorLogico Operador { get; set; } = OperadorLogico.SoloDinero;

    /// <summary>CeroComision | PagoMinimo — qué se paga si NO se cumple.</summary>
    public AccionIncumplimiento AccionIncumplimiento { get; set; } = AccionIncumplimiento.CeroComision;

    /// <summary>Piso garantizado cuando <see cref="AccionIncumplimiento"/> es PagoMinimo.</summary>
    public decimal MontoPagoMinimo { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}

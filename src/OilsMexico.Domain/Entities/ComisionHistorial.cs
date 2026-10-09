using OilsMexico.Domain.Enums;

namespace OilsMexico.Domain.Entities;

/// <summary>
/// Cierre mensual CONGELADO de comisión de un vendedor. Tabla: comisiones_historial.
/// <para>
/// Es una FOTOGRAFÍA, no un cálculo: guarda la cobranza real del periodo, las metas que se
/// aplicaron y el desglose por producto. Si después se edita la cuota, cambia el tabulador o
/// entra un cobro atrasado, estos registros NO se mueven — por eso el cierre es auditable.
/// Para recalcular hay que reabrir el periodo explícitamente.
/// </para>
/// </summary>
public sealed class ComisionHistorial
{
    public int Id { get; set; }

    public int VendedorId { get; set; }
    public Usuario? Vendedor { get; set; }

    public int Anio { get; set; }
    public int Mes { get; set; }

    public EstadoComision Estado { get; set; } = EstadoComision.Borrador;

    // ---- resultado real del periodo (cash basis, viene de CobranzaService) ----
    /// <summary>Dinero cobrado SIN IVA en el periodo (base de comisión).</summary>
    public decimal DineroReal { get; set; }
    /// <summary>Litros efectivamente cobrados en el periodo.</summary>
    public decimal LitrosReales { get; set; }
    /// <summary>Cobro bruto CON IVA (informativo; NO es base de comisión).</summary>
    public decimal CobradoBruto { get; set; }
    public int FacturasCobradas { get; set; }

    // ---- metas aplicadas (copia congelada de cuotas_vendedor) ----
    public decimal? MetaDinero { get; set; }
    public decimal? MetaLitros { get; set; }
    public OperadorLogico Operador { get; set; }
    public AccionIncumplimiento AccionIncumplimiento { get; set; }
    public decimal MontoPagoMinimo { get; set; }

    /// <summary>% de cumplimiento de la meta de dinero (0 si no había meta).</summary>
    public decimal PctDinero { get; set; }
    /// <summary>% de cumplimiento de la meta de litros (0 si no había meta).</summary>
    public decimal PctLitros { get; set; }
    /// <summary>Resultado de evaluar el operador lógico sobre las metas activas.</summary>
    public bool CumplioMeta { get; set; }
    /// <summary>Detalle legible de por qué cumplió o no (para auditoría).</summary>
    public string? DetalleEvaluacion { get; set; }

    // ---- comisión calculada ----
    /// <summary>Suma de base × % base (lo que tocaría si NO cumple).</summary>
    public decimal ComisionBase { get; set; }
    /// <summary>Suma de base × % bono (lo que tocaría si SÍ cumple).</summary>
    public decimal ComisionBono { get; set; }
    /// <summary>Output final del algoritmo (bono si cumple; si no, según accion_incumplimiento).</summary>
    public decimal ComisionFinal { get; set; }
    /// <summary>Piso garantizado aplicado (PagoMinimo).</summary>
    public bool AplicoPagoMinimo { get; set; }
    /// <summary>Productos del periodo SIN porcentaje de comisión configurado.</summary>
    public string? ProductosSinTabulador { get; set; }

    // ---- desglose congelado ----
    /// <summary>
    /// Desglose por producto en JSON: productoId, neto, litros, %aplicado y comisión.
    /// Se guarda serializado para que el histórico sea auditable aunque cambien productos/precios.
    /// </summary>
    public string? DesgloseJson { get; set; }

    public DateTime CalculadoUtc { get; set; } = DateTime.UtcNow;
    public int? CerradoPorUsuarioId { get; set; }
    public DateTime? CerradoUtc { get; set; }
    public DateTime? PagadoUtc { get; set; }
    public string? Notas { get; set; }
}

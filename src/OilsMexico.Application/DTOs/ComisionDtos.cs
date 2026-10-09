using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

// =====================================================================================
// Cuotas (metas mensuales por vendedor)
// =====================================================================================

/// <summary>Meta mensual de un vendedor tal como está configurada hoy.</summary>
public sealed record CuotaDto(
    int Id, int VendedorId, string Vendedor, int Anio, int Mes,
    decimal? MetaDinero, decimal? MetaLitros,
    OperadorLogico Operador, AccionIncumplimiento AccionIncumplimiento,
    decimal MontoPagoMinimo, string? Notas);

/// <summary>
/// Alta/edición de cuota. Los porcentajes de comisión NO van aquí: viven por presentación
/// en <c>unidades_medida</c> (ver <see cref="TabuladorDto"/>).
/// </summary>
public sealed record GuardarCuotaRequest(
    int? Id, int VendedorId, int Anio, int Mes,
    decimal? MetaDinero, decimal? MetaLitros,
    OperadorLogico Operador, AccionIncumplimiento AccionIncumplimiento,
    decimal MontoPagoMinimo, string? Notas);

// =====================================================================================
// Tabulador por presentación
// =====================================================================================

/// <summary>Tabulador de una presentación (producto + unidad).</summary>
public sealed record TabuladorDto(
    int UnidadMedidaId, int ProductoId, string Sku, string Producto, string Unidad,
    decimal FactorLitros, decimal PorcComisionBase, decimal PorcComisionBono);

public sealed record GuardarTabuladorRequest(int UnidadMedidaId, decimal PorcBase, decimal PorcBono);

// =====================================================================================
// Cálculo en vivo (vista del vendedor / panel admin)
// =====================================================================================

/// <summary>Comisión por producto, ya prorrateada al dinero cobrado.</summary>
public sealed record ComisionProductoDto(
    int ProductoId, string Producto, string Unidad,
    decimal NetoSinIva, decimal Litros,
    decimal PorcBase, decimal PorcBono, decimal PorcAplicado,
    decimal ComisionBase, decimal ComisionBono);

/// <summary>
/// Cálculo de comisión SIN congelar: es una vista en vivo del periodo (puede moverse
/// durante el mes porque la cobranza sigue entrando). Para el valor oficial usar el cierre.
/// </summary>
public sealed record ComisionCalculoDto(
    int VendedorId, string Vendedor, int Anio, int Mes,
    bool TieneCuota,
    // Nullable a propósito: «sin meta» es distinto de «meta en 0». Con 0 la UI dibujaría una
    // barra de progreso contra una meta inexistente. null = ese objetivo no aplica.
    decimal? MetaDinero, decimal? MetaLitros,
    OperadorLogico Operador, AccionIncumplimiento AccionIncumplimiento, decimal MontoPagoMinimo,
    decimal DineroReal, decimal LitrosReales, decimal CobradoBruto, int FacturasCobradas,
    decimal PctDinero, decimal PctLitros, bool CumplioMeta, string DetalleEvaluacion,
    decimal ComisionBase, decimal ComisionBono, decimal ComisionFinal, bool AplicoPagoMinimo,
    List<string> ProductosSinTabulador,
    List<ComisionProductoDto> Productos);

// =====================================================================================
// Cierre congelado (comisiones_historial)
// =====================================================================================

/// <summary>Renglón del cierre congelado.</summary>
public sealed record ComisionHistorialDto(
    int Id, int VendedorId, string Vendedor, int Anio, int Mes,
    EstadoComision Estado,
    decimal DineroReal, decimal LitrosReales, decimal CobradoBruto, int FacturasCobradas,
    decimal? MetaDinero, decimal? MetaLitros,
    OperadorLogico Operador, AccionIncumplimiento AccionIncumplimiento, decimal MontoPagoMinimo,
    decimal PctDinero, decimal PctLitros, bool CumplioMeta, string? DetalleEvaluacion,
    decimal ComisionBase, decimal ComisionBono, decimal ComisionFinal, bool AplicoPagoMinimo,
    List<string> ProductosSinTabulador,
    DateTime CalculadoUtc, DateTime? CerradoUtc, DateTime? PagadoUtc, string? Notas);

// =====================================================================================
// Portal del vendedor (Pantalla B del documento)
// =====================================================================================

/// <summary>
/// Todo lo que necesita la pantalla del vendedor en una sola llamada: avance real, proyección
/// a fin de mes (run-rate), potenciales por cobrar y estado del cierre.
/// </summary>
public sealed record PortalVendedorDto(
    ComisionCalculoDto Calculo,
    int DiasTranscurridos, int DiasDelMes,
    /// <summary>Proyección lineal de dinero cobrado a fin de mes con el ritmo actual.</summary>
    decimal ProyeccionDinero,
    /// <summary>Proyección lineal de litros a fin de mes.</summary>
    decimal ProyeccionLitros,
    /// <summary>Comisión que se cobraría si se cobraran todas las facturas pendientes.</summary>
    decimal ComisionPotencialTotal,
    List<PotencialDto> Pendientes,
    /// <summary>Verdadero si el mes ya está congelado: los números no se mueven más.</summary>
    bool PeriodoCerrado,
    EstadoComision? EstadoCierre);

public sealed record CerrarPeriodoRequest(int VendedorId, int Anio, int Mes, string? Notas);

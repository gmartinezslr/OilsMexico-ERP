namespace OilsMexico.Domain.Entities;

/// <summary>
/// Origen ÚNICO de los códigos SAT por defecto del CFDI (ver A8/A9 del handoff de hardcode).
/// Son valores del catálogo del SAT que el negocio no cambia por sucursal: van como constantes
/// tipadas en <c>Domain</c>, NO en tabla. Las entidades (Factura, VentaCobro, CompraPago,
/// NotaCredito) inicializan sus propiedades desde aquí para que un ajuste de default sea de un
/// solo sitio y no haya literales repetidos por el código.
/// </summary>
/// <remarks>
/// No confundir con los catálogos SAT seleccionables (c_FormaPago, c_MetodoPago, c_UsoCFDI),
/// que sí viven en <c>CatalogosSatService</c> y son editables por el usuario en la UI; estas
/// constantes son solo el valor preseleccionado por defecto.
/// </remarks>
public static class CfdiDefaults
{
    /// <summary>Forma de pago por defecto de una venta: 01 = Efectivo (c_FormaPago).</summary>
    public const string FormaPagoVenta = "01";

    /// <summary>Forma de pago por defecto de una compra: 03 = Transferencia electrónica (c_FormaPago).</summary>
    public const string FormaPagoCompra = "03";

    /// <summary>Método de pago por defecto: PUE = Pago en una sola exhibición (c_MetodoPago).</summary>
    public const string MetodoPago = "PUE";

    /// <summary>Uso de CFDI por defecto en ventas: G03 = Gastos en general (c_UsoCFDI).</summary>
    public const string UsoCfdiVenta = "G03";

    /// <summary>Uso de CFDI por defecto en notas de crédito: G02 = Devoluciones, descuentos o bonificaciones (c_UsoCFDI).</summary>
    public const string UsoCfdiNotaCredito = "G02";

    /// <summary>Tipo de relación CFDI por defecto de una nota de crédito: 01 = Nota de crédito de los documentos relacionados (c_TipoRelacion).</summary>
    public const string TipoRelacionNotaCredito = "01";

    /// <summary>
    /// Motivos de cancelación SAT (c_MotivoCancelacion) en el orden en que los muestra la UI.
    /// Fuente única para Facturación y Notas de Crédito (antes duplicados a mano en ambas páginas).
    /// </summary>
    public static readonly IReadOnlyList<(string Codigo, string Descripcion, bool ExigeSustituto)> MotivosCancelacion =
    [
        ("02", "Sin relación", false),
        ("01", "Con relación (exige sustituto)", true),
        ("03", "No se llevó a cabo", false),
        ("04", "Nominativa relacionada", false)
    ];
}

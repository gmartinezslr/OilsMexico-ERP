namespace OilsMexico.Domain.Entities;

/// <summary>
/// Catálogo oficial del SAT o del sistema (c_FormaPago, c_MetodoPago, c_UsoCFDI, c_RegimenFiscal, c_MotivoCancelacion, c_SituacionFiscal).
/// Permite extender o ajustar catálogos sin recompilar código y clasificar formas de pago por grupo (OCP).
/// </summary>
public sealed class CatalogoSat
{
    public int Id { get; set; }

    /// <summary>Identificador del catálogo (ej. "c_FormaPago", "c_MetodoPago", "c_UsoCFDI", "c_RegimenFiscal", "c_MotivoCancelacion", "c_SituacionFiscal").</summary>
    public string Catalogo { get; set; } = string.Empty;

    /// <summary>Clave oficial del SAT o código del sistema (ej. "01", "PUE", "601", "G03").</summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Descripción legible para el usuario.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Agrupación funcional (ej. en formas de pago: "efectivo", "tarjeta", "transferencia", "otro";
    /// en régimen: "moral", "fisica", "mixto").
    /// </summary>
    public string? Grupo { get; set; }

    /// <summary>Indica si el registro está activo y disponible para selección en la UI.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Orden preferente de despliegue en combos y listados.</summary>
    public int Orden { get; set; }
}

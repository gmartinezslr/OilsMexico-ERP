using OilsMexico.Domain.Enums;

namespace OilsMexico.Domain.Entities;

/// <summary>Proveedor de mercancía / compras. Tabla: proveedores.</summary>
public sealed class Proveedor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Rfc { get; set; } = "XAXX010101000";
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    // Dirección desglosada (autocompletada vía SEPOMEX por CP)
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string CodigoPostal { get; set; } = string.Empty;
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string Pais { get; set; } = "México";

    /// <summary>Dirección legacy de texto libre (compatibilidad).</summary>
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;

    public SituacionFiscal SituacionFiscal { get; set; } = SituacionFiscal.Actual; // Estado de Situación Fiscal declarado ante el SAT.
}

namespace OilsMexico.Domain.Entities;

/// <summary>Sucursal física independiente. Tabla: sucursales.</summary>
public sealed class Sucursal
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CodigoSucursal { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string RfcEmisor { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    // Datos fiscales del emisor CFDI 4.0 (necesarios para timbrar: Rfc/Nombre/Regimen/CP).
    public string? RazonSocial { get; set; }
    public string? RegimenFiscal { get; set; }  // c_RegimenFiscal emisor: 601, 612, ...
    public string? CodigoPostal { get; set; }   // CP del domicilio fiscal (emisor)

    // Dirección desglosada (autocompletada vía SEPOMEX por CP)
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string? Pais { get; set; }

    // Contacto de la sucursal
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}

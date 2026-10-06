namespace OilsMexico.Domain.Entities;

public sealed class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Rfc { get; set; } = "XAXX010101000";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    // Dirección desglosada (autocompletada vía SEPOMEX por CP)
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string Pais { get; set; } = "México";
    public bool Activo { get; set; } = true;
    public string TipoPrecio { get; set; } = "menudeo"; // menudeo | mayoreo
    public string RegimenFiscal { get; set; } = "616";  // Sin obligaciones fiscales (default público general)
    public string CodigoPostal { get; set; } = "06600";

    public List<Factura> Facturas { get; set; } = [];
}

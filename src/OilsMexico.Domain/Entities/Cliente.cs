namespace OilsMexico.Domain.Entities;

public sealed class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Rfc { get; set; } = "XAXX010101000";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string TipoPrecio { get; set; } = "menudeo"; // menudeo | mayoreo
    public string RegimenFiscal { get; set; } = "616";  // Sin obligaciones fiscales (default público general)
    public string CodigoPostal { get; set; } = "06600";
}

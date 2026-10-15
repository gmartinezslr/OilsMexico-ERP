namespace OilsMexico.Domain.Entities;

public sealed class DireccionTercero
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public int? ContactoRecibeId { get; set; }
    public ContactoTercero? ContactoRecibe { get; set; }
    public string Tipo { get; set; } = "Principal";
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string Pais { get; set; } = "México";
    public bool EsPredeterminada { get; set; }
    public string? Horario { get; set; }
    public string? Instrucciones { get; set; }
}

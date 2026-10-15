namespace OilsMexico.Domain.Entities;

public sealed class TelefonoTercero
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public int? ContactoTerceroId { get; set; }
    public ContactoTercero? Contacto { get; set; }
    public string Tipo { get; set; } = "Celular";
    public string Numero { get; set; } = string.Empty;
    public bool TieneWhatsApp { get; set; }
    public bool EsPrincipal { get; set; }
    public string? Horario { get; set; }
    public string? Notas { get; set; }
}

namespace OilsMexico.Domain.Entities;

public sealed class ContactoTercero
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Puesto { get; set; }
    public string? Area { get; set; }
    public bool EsPrincipal { get; set; }
    public bool EsRespaldo { get; set; }
    public bool PuedeRecibirPedidos { get; set; }
    public bool PuedeRecibirNotificaciones { get; set; }
    public string? AlcanceAutorizacion { get; set; }
    public string? Horario { get; set; }
    public string? Notas { get; set; }
}

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
}

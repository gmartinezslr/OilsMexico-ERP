namespace OilsMexico.Domain.Entities;

/// <summary>
/// Parámetro clave-valor de configuración global para importación de productos.
/// Almacena la configuración de rutas, zonas, fuente, etc., mediante <c>clave</c> única.
/// </summary>
public sealed class ImportacionParametro
{
    public int Id { get; set; }
    /// <summary>Clave única del parámetro (p. ej. RutaCarpeta, Zona, FechaInicio).</summary>
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
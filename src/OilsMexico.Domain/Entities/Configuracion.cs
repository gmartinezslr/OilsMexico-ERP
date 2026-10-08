namespace OilsMexico.Domain.Entities;

/// <summary>
/// Parámetro de configuración del sistema persistido como pares clave/valor
/// (p. ej. <c>sesion_timeout_min</c> = minutos de inactividad antes de cerrar sesión).
/// </summary>
public sealed class Configuracion
{
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}

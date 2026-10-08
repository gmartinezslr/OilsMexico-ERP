namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Acceso a los parámetros de configuración del sistema persistidos en BD (pares clave/valor).
/// </summary>
public interface IConfiguracionService
{
    /// <summary>Minutos de inactividad antes de cerrar la sesión. Devuelve 5 si no está configurado o el valor no es válido.</summary>
    Task<int> ObtenerMinutosTimeoutAsync(CancellationToken ct = default);

    /// <summary>Guarda los minutos de inactividad (se limita al rango 1–480).</summary>
    Task<int> GuardarMinutosTimeoutAsync(int minutos, CancellationToken ct = default);
}

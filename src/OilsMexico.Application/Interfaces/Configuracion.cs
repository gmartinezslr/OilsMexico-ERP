using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Acceso a los parámetros de configuración del sistema persistidos en BD (pares clave/valor).
/// API genérica + accesos tipados; cualquier parámetro nuevo del ERP se agrega como una clave
/// más sin tener que ampliar este contrato (OCP).
/// </summary>
public interface IConfiguracionService
{
    /// <summary>Minutos de inactividad antes de cerrar la sesión. Devuelve 5 si no está configurado o el valor no es válido.</summary>
    Task<int> ObtenerMinutosTimeoutAsync(CancellationToken ct = default);

    /// <summary>Guarda los minutos de inactividad (se limita al rango 1–480).</summary>
    Task<int> GuardarMinutosTimeoutAsync(int minutos, CancellationToken ct = default);

    /// <summary>Lee una clave numérica. Devuelve <paramref name="defecto"/> si la clave falta o el valor está fuera de [<paramref name="minimo"/>, <paramref name="maximo"/>].</summary>
    Task<int> ObtenerIntAsync(string clave, int defecto, int minimo, int maximo, CancellationToken ct = default);

    /// <summary>Guarda una clave textual (inserta si falta; actualiza si existe).</summary>
    Task GuardarAsync(string clave, string valor, CancellationToken ct = default);

    /// <summary>Política de bloqueo por intentos de login (login_max_intentos, login_minutos_bloqueo, login_bloqueos_para_definitivo).</summary>
    Task<PoliticaBloqueoDto> ObtenerPoliticaBloqueoAsync(CancellationToken ct = default);

    /// <summary>Guarda la política de bloqueo (los valores se normalizan al rango permitido).</summary>
    Task<PoliticaBloqueoDto> GuardarPoliticaBloqueoAsync(int maxIntentos, int minutosBloqueo, int bloqueosParaDefinitivo, CancellationToken ct = default);
}

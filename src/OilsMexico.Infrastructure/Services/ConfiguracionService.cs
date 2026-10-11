using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Lee y escribe los parámetros de configuración del sistema (tabla <c>configuracion</c>).
/// API genérica (ObtenerIntAsync/GuardarAsync) + accesos tipados para los parámetros actuales.
/// </summary>
public sealed class ConfiguracionService(ErpDbContext db) : IConfiguracionService
{
    public const string ClaveTimeoutSesion = "sesion_timeout_min";
    public const string ClavePasswordMinLongitud = "password_policy_min_longitud";
    public const string ClavePasswordHistorial = "password_policy_historial";
    public const string ClavePasswordDuracionDias = "password_policy_duracion_dias";
    // Política de bloqueo por intentos de login (antes hardcodeada en AuthService).
    public const string ClaveLoginMaxIntentos = "login_max_intentos";
    public const string ClaveLoginMinutosBloqueo = "login_minutos_bloqueo";
    public const string ClaveLoginBloqueosDefinitivo = "login_bloqueos_para_definitivo";
    // Umbrales del análisis de rotación ABC (fracciones del ingreso acumulado; antes fijos en GestionService).
    public const string ClaveAbcUmbralA = "abc_umbral_a";
    public const string ClaveAbcUmbralB = "abc_umbral_b";
    private const int TimeoutPorDefecto = 5;
    private const int TimeoutMin = 1;
    private const int TimeoutMax = 480; // 8 horas

    // Defaults de la política de bloqueo (coinciden con el comportamiento histórico: 3 fallos →
    // bloqueo de 30 minutos; el 2.º bloqueo es definitivo).
    private const int BloqueoIntentosPorDefecto = 3;
    private const int BloqueoMinutosPorDefecto = 30;
    private const int BloqueoDefinitivoPorDefecto = 2;

    // Defaults del análisis ABC (corte histórico 30%/70%).
    private const decimal AbcUmbralAPorDefecto = 0.30m;
    private const decimal AbcUmbralBPorDefecto = 0.70m;

    public async Task<int> ObtenerMinutosTimeoutAsync(CancellationToken ct = default)
        => await ObtenerIntAsync(ClaveTimeoutSesion, TimeoutPorDefecto, TimeoutMin, TimeoutMax, ct);

    public async Task<int> GuardarMinutosTimeoutAsync(int minutos, CancellationToken ct = default)
    {
        minutos = Math.Clamp(minutos, TimeoutMin, TimeoutMax);
        await GuardarAsync(ClaveTimeoutSesion, minutos.ToString(), ct);
        return minutos;
    }

    public async Task<int> ObtenerIntAsync(string clave, int defecto, int minimo, int maximo, CancellationToken ct = default)
    {
        var valor = await db.Configuracion.AsNoTracking()
            .Where(c => c.Clave == clave)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync(ct);
        return int.TryParse(valor, out var v) && v >= minimo && v <= maximo ? v : defecto;
    }

    public async Task<decimal> ObtenerDecimalAsync(string clave, decimal defecto, decimal minimo, decimal maximo, CancellationToken ct = default)
    {
        var valor = await db.Configuracion.AsNoTracking()
            .Where(c => c.Clave == clave)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync(ct);
        var separadores = System.Globalization.CultureInfo.InvariantCulture;
        return decimal.TryParse(valor, System.Globalization.NumberStyles.Number, separadores, out var v)
            && v >= minimo && v <= maximo ? v : defecto;
    }

    public async Task<(decimal UmbralA, decimal UmbralB)> ObtenerUmbralesAbcAsync(CancellationToken ct = default)
    {
        var a = await ObtenerDecimalAsync(ClaveAbcUmbralA, AbcUmbralAPorDefecto, 0.01m, 0.99m, ct);
        // B debe ser siempre mayor que A; si el administrador dejó B<=A, se normaliza A+0.01.
        var b = await ObtenerDecimalAsync(ClaveAbcUmbralB, AbcUmbralBPorDefecto, 0.01m, 0.99m, ct);
        if (b <= a) b = Math.Min(0.99m, a + 0.01m);
        return (a, b);
    }

    public async Task GuardarAsync(string clave, string valor, CancellationToken ct = default)
    {
        var existente = await db.Configuracion.FirstOrDefaultAsync(c => c.Clave == clave, ct);
        if (existente is null)
            db.Configuracion.Add(new Configuracion { Clave = clave, Valor = valor });
        else
            existente.Valor = valor;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PoliticaBloqueoDto> ObtenerPoliticaBloqueoAsync(CancellationToken ct = default)
    {
        var maxIntentos = await ObtenerIntAsync(ClaveLoginMaxIntentos, BloqueoIntentosPorDefecto, 1, 10, ct);
        var minutos = await ObtenerIntAsync(ClaveLoginMinutosBloqueo, BloqueoMinutosPorDefecto, 1, 1440, ct);
        var definitivo = await ObtenerIntAsync(ClaveLoginBloqueosDefinitivo, BloqueoDefinitivoPorDefecto, 1, 10, ct);
        return new PoliticaBloqueoDto(maxIntentos, minutos, definitivo);
    }

    public async Task<PoliticaBloqueoDto> GuardarPoliticaBloqueoAsync(
        int maxIntentos, int minutosBloqueo, int bloqueosParaDefinitivo, CancellationToken ct = default)
    {
        var politica = new PoliticaBloqueoDto(
            Math.Clamp(maxIntentos, 1, 10),
            Math.Clamp(minutosBloqueo, 1, 1440),
            Math.Clamp(bloqueosParaDefinitivo, 1, 10));
        await GuardarAsync(ClaveLoginMaxIntentos, politica.MaxIntentos.ToString(), ct);
        await GuardarAsync(ClaveLoginMinutosBloqueo, politica.MinutosBloqueo.ToString(), ct);
        await GuardarAsync(ClaveLoginBloqueosDefinitivo, politica.BloqueosParaDefinitivo.ToString(), ct);
        return politica;
    }
}

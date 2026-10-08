using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Lee y escribe los parámetros de configuración del sistema (tabla <c>configuracion</c>).
/// </summary>
public sealed class ConfiguracionService(ErpDbContext db) : IConfiguracionService
{
    public const string ClaveTimeoutSesion = "sesion_timeout_min";
    private const int TimeoutPorDefecto = 5;
    private const int TimeoutMin = 1;
    private const int TimeoutMax = 480; // 8 horas

    public async Task<int> ObtenerMinutosTimeoutAsync(CancellationToken ct = default)
    {
        var valor = await db.Configuracion.AsNoTracking()
            .Where(c => c.Clave == ClaveTimeoutSesion)
            .Select(c => c.Valor)
            .FirstOrDefaultAsync(ct);
        return Normalizar(valor);
    }

    public async Task<int> GuardarMinutosTimeoutAsync(int minutos, CancellationToken ct = default)
    {
        minutos = Math.Clamp(minutos, TimeoutMin, TimeoutMax);
        var existente = await db.Configuracion.FirstOrDefaultAsync(c => c.Clave == ClaveTimeoutSesion, ct);
        if (existente is null)
        {
            db.Configuracion.Add(new Configuracion { Clave = ClaveTimeoutSesion, Valor = minutos.ToString() });
        }
        else
        {
            existente.Valor = minutos.ToString();
        }
        await db.SaveChangesAsync(ct);
        return minutos;
    }

    private static int Normalizar(string? valor)
        => int.TryParse(valor, out var min) && min >= TimeoutMin && min <= TimeoutMax ? min : TimeoutPorDefecto;
}

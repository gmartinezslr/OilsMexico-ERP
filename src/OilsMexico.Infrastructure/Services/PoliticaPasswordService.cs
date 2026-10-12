using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Única fuente de la política de contraseñas (longitud, complejidad, hash). Antes la
/// página Usuarios repetía las reglas de complejidad campo por campo y llamaba a
/// <c>AuthService.Hash</c>; ahora depende de <see cref="IPoliticaPassword"/> (ISP) y la
/// validación vive en un solo sitio (SRP), reutilizada por <c>AuthService</c>.
/// </summary>
public sealed class PoliticaPasswordService(ErpDbContext db) : IPoliticaPassword
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    /// <summary>Valores por defecto cuando aún no hay política guardada en configuración.</summary>
    public static PasswordConfigDto PorDefecto => new(20, 3, 90, true, true, true, true);

    public Task<PasswordConfigDto> ObtenerAsync(CancellationToken ct = default) => ObtenerAsync(db, ct);

    /// <summary>
    /// Lee la política persistida en la tabla <c>configuracion</c> (clave/valor). Misma
    /// lógica que usaba <c>AuthService.ObtenerPasswordConfigAsync</c>, ahora reutilizable.
    /// </summary>
    public static async Task<PasswordConfigDto> ObtenerAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var fila = await db.Configuracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Clave == ConfiguracionService.ClavePasswordMinLongitud, ct);
        int min = fila?.Valor != null && int.TryParse(fila.Valor, out var v) && v >= 4 ? v : 20;
        fila = await db.Configuracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Clave == ConfiguracionService.ClavePasswordHistorial, ct);
        int hist = fila?.Valor != null && int.TryParse(fila.Valor, out var h) && h >= 0 ? h : 3;
        fila = await db.Configuracion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Clave == ConfiguracionService.ClavePasswordDuracionDias, ct);
        int dur = fila?.Valor != null && int.TryParse(fila.Valor, out var d) && d >= 0 ? d : 90;
        return new PasswordConfigDto(min, hist, dur, true, true, true, true);
    }

    public async Task<(bool Valida, List<string> Fallos)> ValidarAsync(string password, CancellationToken ct = default)
    {
        var cfg = await ObtenerAsync(ct);
        return Validar(password, cfg);
    }

    /// <summary>
    /// Valida una contraseña candidata contra una política concreta y devuelve la lista de
    /// fallos (vacía si cumple) para mostrarlos todos juntos en el formulario.
    /// </summary>
    public static (bool Valida, List<string> Fallos) Validar(string password, PasswordConfigDto cfg)
    {
        var fallos = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            fallos.Add("la contraseña no puede estar vacía");
            return (false, fallos);
        }
        if (password.Length < cfg.LongitudMinima || password.Length > 100)
            fallos.Add($"debe tener de {cfg.LongitudMinima} a 100 caracteres");
        if (cfg.RequiereMayusculas && !password.Any(char.IsUpper)) fallos.Add("falta una mayúscula");
        if (cfg.RequiereMinusculas && !password.Any(char.IsLower)) fallos.Add("falta una minúscula");
        if (cfg.RequiereNumeros && !password.Any(char.IsDigit)) fallos.Add("falta un número");
        if (cfg.RequiereEspecial && !password.Any(c => !char.IsLetterOrDigit(c))) fallos.Add("falta un carácter especial");
        return (fallos.Count == 0, fallos);
    }

    public string Hash(string password) => Hasher.HashPassword(new Usuario(), password);
}


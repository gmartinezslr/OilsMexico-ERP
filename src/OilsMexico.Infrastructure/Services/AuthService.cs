using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using OilsMexico.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class AuthService(ErpDbContext db, IConfiguration configuration, ILogger<AuthService> logger) : IAuthService
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    public async Task<LoginResultado> LoginAsync(string identificador, string password, CancellationToken ct = default)
    {
        var id = identificador.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
            return new(null, "Escribe tu usuario y contraseña.");
        var u = await db.Usuarios.Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Nombre.ToLower() == id.ToLower() ||
                (x.Correo != null && x.Correo.ToLower() == id.ToLower()), ct);
        if (u is null) return new(null, "Usuario o contraseña incorrectos.");
        var ahora = DateTime.UtcNow;
        if (!u.Activo) return new(null, "Usuario inactivo. Contacta al administrador.");
        if (u.BloqueadoDefinitivamente) return new(null, "Usuario bloqueado definitivamente. Contacta al administrador.");
        if (u.BloqueadoHastaUtc is { } hasta && hasta > ahora)
            return new(null, $"Usuario bloqueado hasta {hasta.ToLocalTime():g}.", hasta);


        if (!await VerificarConUpgradeAsync(u, password, ct))
        {
            u.IntentosFallidos++;
            if (u.IntentosFallidos >= 3)
            {
                u.IntentosFallidos = 0;
                u.BloqueosTemporales++;
                if (u.BloqueosTemporales >= 2)
                {
                    u.BloqueadoDefinitivamente = true;
                    await db.SaveChangesAsync(ct);
                    await NotificarAdministradorAsync(u.Nombre, id);
                    return new(null, "Usuario bloqueado definitivamente por intentos fallidos. Se notificó al administrador.");
                }
                u.BloqueadoHastaUtc = ahora.AddMinutes(30);
                await db.SaveChangesAsync(ct);
                return new(null, "Tres intentos fallidos. Usuario bloqueado por 30 minutos.", u.BloqueadoHastaUtc);
            }
            await db.SaveChangesAsync(ct);
            return new(null, $"Usuario o contraseña incorrectos. Intento {u.IntentosFallidos} de 3.");
        }

        u.IntentosFallidos = 0;
        u.BloqueadoHastaUtc = null;
        u.SesionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await db.SaveChangesAsync(ct);
        return new(new SesionDto(u.Id, u.Nombre, u.Rol, u.SucursalId, u.Sucursal?.Nombre ?? "", u.SesionToken));
    }

    public Task<SesionDto?> LoginPorPinAsync(string pin, CancellationToken ct = default) => Task.FromResult<SesionDto?>(null);

    public async Task<bool> DesbloquearUsuarioAsync(int usuarioId, CancellationToken ct = default)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is null) return false;
        u.IntentosFallidos = 0;
        u.BloqueosTemporales = 0;
        u.BloqueadoHastaUtc = null;
        u.BloqueadoDefinitivamente = false;
        u.SesionToken = null; // Revoca las sesiones abiertas del usuario desbloqueado.
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task NotificarAdministradorAsync(string nombre, string identificador)
    {
        var correoAdmin = configuration["Auth:CorreoAdministrador"];
        var smtpHost = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(correoAdmin) || string.IsNullOrWhiteSpace(smtpHost))
        {
            logger.LogWarning("Cuenta {Usuario} bloqueada definitivamente. Configure Auth:CorreoAdministrador y Email SMTP para notificar.", nombre);
            return;
        }
        try
        {
            using var smtp = new System.Net.Mail.SmtpClient(smtpHost, int.TryParse(configuration["Email:Port"], out var port) ? port : 587)
            {
                EnableSsl = !bool.TryParse(configuration["Email:EnableSsl"], out var ssl) || ssl
            };
            if (!string.IsNullOrWhiteSpace(configuration["Email:Username"]))
                smtp.Credentials = new System.Net.NetworkCredential(configuration["Email:Username"], configuration["Email:Password"]);
            var remitente = configuration["Email:From"] ?? configuration["Email:Username"];
            if (string.IsNullOrWhiteSpace(remitente)) throw new InvalidOperationException("Email:From is required.");
            using var mensaje = new System.Net.Mail.MailMessage(remitente, correoAdmin,
                "Cuenta OilsMexico bloqueada definitivamente", $"La cuenta {nombre} ({identificador}) fue bloqueada tras un segundo bloqueo por intentos de acceso fallidos. Un administrador debe desbloquearla desde Usuarios.");
            await smtp.SendMailAsync(mensaje);
        }
        catch (Exception ex) { logger.LogError(ex, "No se pudo notificar al administrador del bloqueo de {Usuario}.", nombre); }
    }

    /// <summary>Hash actual: ASP.NET Core Identity (PBKDF2 con sal y coste adaptativo).</summary>
    public static string Hash(string valor) => Hasher.HashPassword(new Usuario(), valor);

    /// <summary>
    /// Verifica la contraseña y migra el almacenamiento heredado (SHA-256 sin sal o PIN legado)
    /// al hash de Identity. El PIN legado solo se acepta cuando la cuenta aún no tiene contraseña.
    /// </summary>
    private async Task<bool> VerificarConUpgradeAsync(Usuario u, string password, CancellationToken ct)
    {
        var hash = u.PasswordHash ?? "";
        var esSha256 = hash.Length == 64 && hash.All(Uri.IsHexDigit);
        if (esSha256)
        {
            // Cuentas heredadas: SHA-256 sin sal, sólo para migrarlas al hash de Identity.
            if (!VerificarSha256(password, hash)) return false;
            u.PasswordHash = Hasher.HashPassword(u, password);
            u.PinHash = string.Empty;
            await db.SaveChangesAsync(ct);
            return true;
        }
        if (!string.IsNullOrEmpty(hash))
        {
            // Formato ASP.NET Core Identity (v2$, v3$ o marcador binario).
            var res = Hasher.VerifyHashedPassword(u, hash, password);
            if (res == PasswordVerificationResult.Failed) return false;
            if (res == PasswordVerificationResult.SuccessRehashNeeded)
            {
                u.PasswordHash = Hasher.HashPassword(u, password);
                await db.SaveChangesAsync(ct);
            }
            return true;
        }

        // PIN legado: sólo cuando la cuenta aún no tiene contraseña.
        if (!VerificarSha256(password, u.PinHash)) return false;
        u.PasswordHash = Hasher.HashPassword(u, password);
        u.PinHash = string.Empty;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string Sha256Hex(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
    private static bool VerificarSha256(string valor, string hash) => !string.IsNullOrEmpty(hash) &&
        hash.Length == 64 && hash.All(Uri.IsHexDigit) &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sha256Hex(valor)), Convert.FromHexString(hash));
}

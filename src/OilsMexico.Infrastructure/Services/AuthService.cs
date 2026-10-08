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
    // Hash de referencia para igualar el tiempo de respuesta cuando la cuenta no existe (evita enumerar).
    private static readonly string HashDummy = Hasher.HashPassword(new Usuario(), "oilsmexico-timing");
    private const string ErrorCredenciales = "Usuario o contraseña incorrectos.";

    public async Task<LoginResultado> LoginAsync(string identificador, string password, CancellationToken ct = default)
    {
        var id = identificador.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
            return new(null, "Escribe tu usuario y contraseña.");
        // AsNoTracking: siempre lee valores frescos de la BD (no usa el caché del circuito).
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Nombre.ToLower() == id.ToLower() ||
                (x.Correo != null && x.Correo.ToLower() == id.ToLower()), ct);
        if (u is null)
        {
            Hasher.VerifyHashedPassword(new Usuario(), HashDummy, password);
            return new(null, ErrorCredenciales);
        }
        // Primero la contraseña: el estado de la cuenta (inactiva/bloqueada) solo se revela a
        // quien la prueba correctamente, para no confirmar la existencia de cuentas.
        if (!await VerificarConUpgradeAsync(u, password, ct))
        {
            await RegistrarIntentoFallidoAsync(u, id, ct);
            return new(null, ErrorCredenciales);
        }

        // Contraseña correcta: un único UPDATE con condiciones decide el acceso con la verdad de la BD.
        var ahora = DateTime.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var concedido = await db.Usuarios
            .Where(x => x.Id == u.Id && x.Activo && !x.BloqueadoDefinitivamente
                && (x.BloqueadoHastaUtc == null || x.BloqueadoHastaUtc <= ahora))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IntentosFallidos, 0)
                // Decisión: un login exitoso limpia intentos y contador de bloqueos. Un atacante
                // que nunca acierta la contraseña nunca limpia el contador, por lo que su segundo
                // bloqueo sigue siendo definitivo; el usuario legítimo que ya autenticó empieza limpio.
                .SetProperty(x => x.BloqueosTemporales, 0)
                .SetProperty(x => x.BloqueadoHastaUtc, (DateTime?)null)
                .SetProperty(x => x.SesionToken, token), ct);
        if (concedido > 0)
            return new(new SesionDto(u.Id, u.Nombre, u.Rol, u.SucursalId, u.Sucursal?.Nombre ?? "", token));

        // Las guardas fallaron: el estado real de la BD decide el mensaje.
        var f = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == u.Id, ct);
        if (f is null) return new(null, ErrorCredenciales);
        if (!f.Activo) return new(null, "La cuenta está inactiva. Contacta al administrador.");
        if (f.BloqueadoDefinitivamente)
            return new(null, "La cuenta está bloqueada por intentos fallidos. El administrador debe desbloquearla.");
        if (f.BloqueadoHastaUtc is { } hasta && hasta > ahora)
            return new(null, $"La cuenta está bloqueada hasta {hasta.ToLocalTime():g}. Espera a que venza o contacta al administrador.", hasta);
        return new(null, ErrorCredenciales);
    }

    /// <summary>
    /// Registra un intento fallido con un único UPDATE atómico (las condiciones van en la sentencia,
    /// no en memoria): intentos simultáneos no pueden evadir el límite de tres. Al tercer fallo
    /// bloquea 30 minutos; si es el segundo bloqueo, bloquea definitivamente y notifica al
    /// administrador. Solo cuenta si la cuenta sigue activa, sin bloqueo vigente ni definitivo.
    /// </summary>
    private async Task RegistrarIntentoFallidoAsync(Usuario u, string identificador, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var bloqueoDefinitivo = false;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var afectadas = await db.Usuarios
                .Where(x => x.Id == u.Id && x.Activo && !x.BloqueadoDefinitivamente
                    && (x.BloqueadoHastaUtc == null || x.BloqueadoHastaUtc <= ahora))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IntentosFallidos, x => x.IntentosFallidos + 1 >= 3 ? 0 : x.IntentosFallidos + 1)
                    .SetProperty(x => x.BloqueosTemporales, x => x.IntentosFallidos + 1 >= 3 ? x.BloqueosTemporales + 1 : x.BloqueosTemporales)
                    .SetProperty(x => x.BloqueadoDefinitivamente, x => x.BloqueadoDefinitivamente || (x.IntentosFallidos + 1 >= 3 && x.BloqueosTemporales + 1 >= 2))
                    .SetProperty(x => x.BloqueadoHastaUtc, x => x.IntentosFallidos + 1 >= 3
                        ? (x.BloqueosTemporales + 1 >= 2 ? x.BloqueadoHastaUtc : (DateTime?)ahora.AddMinutes(30))
                        : x.BloqueadoHastaUtc), ct);
            if (afectadas == 0) return; // Cuenta excluida por las guardas (inactiva, bloqueada o ya definitiva).
            // La transacción retiene la fila: esta lectura refleja exactamente el resultado de este intento.
            bloqueoDefinitivo = await db.Usuarios.AsNoTracking()
                .Where(x => x.Id == u.Id)
                .Select(x => x.BloqueadoDefinitivamente)
                .SingleAsync(ct);
            await tx.CommitAsync(ct);
        }
        if (bloqueoDefinitivo)
            await NotificarAdministradorAsync(u.Nombre, identificador);
    }

    public Task<SesionDto?> LoginPorPinAsync(string pin, CancellationToken ct = default) => Task.FromResult<SesionDto?>(null);

    public async Task<bool> DesbloquearUsuarioAsync(int usuarioId, CancellationToken ct = default)
    {
        // UPDATE directo (no change tracker): el contexto puede tener la entidad rastreada con
        // valores obsoletos (p. ej. la lista de Usuarios), y en ese caso SaveChanges no escribiría.
        var filas = await db.Usuarios.Where(x => x.Id == usuarioId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IntentosFallidos, 0)
                .SetProperty(x => x.BloqueosTemporales, 0)
                .SetProperty(x => x.BloqueadoHastaUtc, (DateTime?)null)
                .SetProperty(x => x.BloqueadoDefinitivamente, false)
                .SetProperty(x => x.SesionToken, (string?)null), ct); // Revoca las sesiones abiertas.
        return filas > 0;
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
            await MigrarHashAsync(u, password, ct);
            return true;
        }
        if (!string.IsNullOrEmpty(hash))
        {
            // Formato ASP.NET Core Identity (v2$, v3$ o marcador binario).
            var res = Hasher.VerifyHashedPassword(u, hash, password);
            if (res == PasswordVerificationResult.Failed) return false;
            if (res == PasswordVerificationResult.SuccessRehashNeeded)
                await MigrarHashAsync(u, password, ct);
            return true;
        }

        // PIN legado: sólo cuando la cuenta aún no tiene contraseña.
        if (!VerificarSha256(password, u.PinHash)) return false;
        await MigrarHashAsync(u, password, ct);
        return true;
    }

    /// <summary>
    /// Migra el hash heredado (SHA-256 o PIN) al hash de Identity. El usuario se carga con
    /// AsNoTracking, por lo que se adjunta al contexto solo para persistir la migración.
    /// </summary>
    private async Task MigrarHashAsync(Usuario u, string password, CancellationToken ct)
    {
        if (db.Entry(u).State == EntityState.Detached) db.Attach(u);
        u.PasswordHash = Hasher.HashPassword(u, password);
        u.PinHash = string.Empty;
        await db.SaveChangesAsync(ct);
    }

    private static string Sha256Hex(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
    private static bool VerificarSha256(string valor, string hash) => !string.IsNullOrEmpty(hash) &&
        hash.Length == 64 && hash.All(Uri.IsHexDigit) &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sha256Hex(valor)), Convert.FromHexString(hash));
}

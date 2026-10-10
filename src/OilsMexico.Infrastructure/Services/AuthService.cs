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
using OtpNet;
using QRCoder;
using System.Drawing;

namespace OilsMexico.Infrastructure.Services;

public sealed class AuthService(ErpDbContext db, IConfiguration configuration, ILogger<AuthService> logger, ISucursalContext ctx, IConfiguracionService configuracion) : IAuthService
{
    private static readonly PasswordHasher<Usuario> Hasher = new();
    // Hash de referencia para igualar el tiempo de respuesta cuando la cuenta no existe (evita enumerar).
    private static readonly string HashDummy = Hasher.HashPassword(new Usuario(), "oilsmexico-timing");
    private const string ErrorCredenciales = "Usuario o contraseña incorrectos.";

    public async Task<LoginResultado> LoginAsync(string identificador, string password, string? codigo2fa = null, CancellationToken ct = default)
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

        // 2FA (opcional por cuenta): si está habilitado, exige el código TOTP ANTES de otorgar sesión.
        if (u.DosFaActivo)
        {
            if (string.IsNullOrWhiteSpace(codigo2fa))
                return new(null, "Este usuario tiene el código de dos factores (2FA) activo. Ingresa el código de tu aplicación.", null, RequiereDosFa: true, UsuarioId: u.Id);
            if (!await ValidarCodigo2FAAsync(u, codigo2fa, ct))
            {
                RegistrarAuditLog(u.Id, "DosFaLoginFallido", detalle: "Intento de acceso con código 2FA inválido.");
                return new(null, "El código de dos factores no es válido. Verifica la hora de tu dispositivo e intenta de nuevo.", null, RequiereDosFa: true, UsuarioId: u.Id);
            }
        }

        // Contraseña (y 2FA si aplica) correctas: expiración por política configurable de duración.
        if (u.UltimoCambioPasswordUtc is { } ultima)
        {
            var cfg = await ObtenerPasswordConfigAsync(ct);
            if (cfg.DuracionDias > 0 && ultima.AddDays(cfg.DuracionDias) <= DateTime.UtcNow)
                return new(null, $"Tu contraseña venció (vigencia de {cfg.DuracionDias} días). Debes cambiarla para continuar.", null, PasswordExpirada: true, UsuarioId: u.Id);
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
    /// no en memoria): intentos simultáneos no pueden evadir el límite de intentos. Al llegar al
    /// máximo configurado bloquea los minutos indicados; si es el bloqueo número N (configurable),
    /// bloquea definitivamente y notifica al administrador. Solo cuenta si la cuenta sigue activa,
    /// sin bloqueo vigente ni definitivo. Los tres parámetros viven en la tabla configuracion
    /// (IConfiguracionService.ObtenerPoliticaBloqueoAsync); los valores por defecto son 3 / 30 / 2.
    /// </summary>
    private async Task RegistrarIntentoFallidoAsync(Usuario u, string identificador, CancellationToken ct)
    {
        var politica = await configuracion.ObtenerPoliticaBloqueoAsync(ct);
        var maxIntentos = politica.MaxIntentos;
        var minutosBloqueo = politica.MinutosBloqueo;
        var bloqueosParaDefinitivo = politica.BloqueosParaDefinitivo;
        var ahora = DateTime.UtcNow;
        var bloqueoDefinitivo = false;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var afectadas = await db.Usuarios
                .Where(x => x.Id == u.Id && x.Activo && !x.BloqueadoDefinitivamente
                    && (x.BloqueadoHastaUtc == null || x.BloqueadoHastaUtc <= ahora))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IntentosFallidos, x => x.IntentosFallidos + 1 >= maxIntentos ? 0 : x.IntentosFallidos + 1)
                    .SetProperty(x => x.BloqueosTemporales, x => x.IntentosFallidos + 1 >= maxIntentos ? x.BloqueosTemporales + 1 : x.BloqueosTemporales)
                    .SetProperty(x => x.BloqueadoDefinitivamente, x => x.BloqueadoDefinitivamente || (x.IntentosFallidos + 1 >= maxIntentos && x.BloqueosTemporales + 1 >= bloqueosParaDefinitivo))
                    .SetProperty(x => x.BloqueadoHastaUtc, x => x.IntentosFallidos + 1 >= maxIntentos
                        ? (x.BloqueosTemporales + 1 >= bloqueosParaDefinitivo ? x.BloqueadoHastaUtc : (DateTime?)ahora.AddMinutes(minutosBloqueo))
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

    private sealed class ResultadoValidacion(string mensaje) { public string Mensaje { get; } = mensaje; public bool IsValid => string.IsNullOrEmpty(mensaje); }

    private (bool IsValid, string Mensaje) ValidarComplejidad(string contrasena, PasswordConfigDto cfg)
    {
        var errores = new List<string>();
        if (contrasena.Length < cfg.LongitudMinima)
            errores.Add($"Mínimo {cfg.LongitudMinima} caracteres.");

        if (cfg.RequiereMayusculas && !contrasena.Any(char.IsUpper))
            errores.Add("Se requiere al menos una letra mayúscula.");

        if (cfg.RequiereMinusculas && !contrasena.Any(char.IsLower))
            errores.Add("Se requiere al menos una letra minúscula.");

        if (cfg.RequiereNumeros && !contrasena.Any(char.IsDigit))
            errores.Add("Se requiere al menos un dígito numérico.");

        if (cfg.RequiereEspecial && !contrasena.Any(c => !char.IsLetterOrDigit(c)))
            errores.Add("Se requiere al menos un carácter especial (ñ,!,@,#,$,%,&,*,?,_,.,-,+,=).");

        return errores.Count == 0
            ? (true, "")
            : (false, "Contraseña inválida: " + string.Join(" ", errores));
    }

    private string GenerarSecretarioTotp()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encoding.ToString(bytes);
    }

    private string GenerarUrlQr(Usuario u)
    {
        var issuer = "OilsMexico";
        var etiqueta = $"{issuer}:{u.Correo ?? $"usuario{u.Id}"}";
        var secret = u.DosFaSecret ?? "";
        return $"otpauth://totp/{Uri.EscapeDataString(etiqueta)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
    }

    private string GenerarQrImagenBase64(string qrUrl)
    {
        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
        var code = new QRCoder.QRCode(data);
        // Usar 16px por módulo para que el QR sea legible en la pantalla de configuración.
        using var bitmap = code.GetGraphic(16);
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static Task<bool> ValidarCodigo2FAAsync(Usuario u, string codigo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(u.DosFaSecret) || string.IsNullOrWhiteSpace(codigo))
            return Task.FromResult(false);

        var secretBytes = Base32Encoding.ToBytes(u.DosFaSecret);
        var totp = new Totp(secretBytes, 30, OtpHashMode.Sha1, 6, null);
        var ok = totp.VerifyTotp(codigo.Trim(), out _, new VerificationWindow(1, 1));
        return Task.FromResult(ok);
    }

    public Task<ConsultaRfcResult> ValidarRfcAsync(string rfc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = RfcValidador.Validar(rfc);
        // Auditoría de validaciones fallidas (trazabilidad de captura fiscal).
        if (!result.Valido)
            RegistrarAuditLog(ctx.Establecida ? ctx.UsuarioId : 0, "RfcValidationFailed",
                detalle: $"RFC '{rfc ?? ""}' no válido: {result.Mensaje}", nuevo: rfc ?? "");
        return Task.FromResult(result);
    }

    public async Task<PasswordConfigDto> ObtenerPasswordConfigAsync(CancellationToken ct = default)
    {
        var fila = await db.Configuracion.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == ConfiguracionService.ClavePasswordMinLongitud, ct);
        int min = fila?.Valor != null && int.TryParse(fila.Valor, out var v) && v >= 4 ? v : 20;
        fila = await db.Configuracion.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == ConfiguracionService.ClavePasswordHistorial, ct);
        int hist = fila?.Valor != null && int.TryParse(fila.Valor, out var h) && h >= 0 ? h : 3;
        fila = await db.Configuracion.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == ConfiguracionService.ClavePasswordDuracionDias, ct);
        int dur = fila?.Valor != null && int.TryParse(fila.Valor, out var d) && d >= 0 ? d : 90;
        return new PasswordConfigDto(min, hist, dur, true, true, true, true);
    }

    public async Task<CambioPasswordResult> GuardarPasswordConfigAsync(PasswordConfigRequest request, int usuarioId, CancellationToken ct = default)
    {
        var esAdmin = (ctx.Establecida ? ctx.Rol : null) == "Admin";
        if (!esAdmin) return new(false, "Solo el administrador puede guardar la política de contraseña.");

        var valido = request.LongitudMinima >= 4 && request.LongitudMinima <= 100
            && request.Historial >= 0 && request.Historial <= 20
            && request.DuracionDias >= 0 && request.DuracionDias <= 3650;
        if (!valido)
            return new(false, "Valores fuera de rango permitido.");

        var guardar = async (string clave, string valor) =>
        {
            var existente = await db.Configuracion.FirstOrDefaultAsync(c => c.Clave == clave, ct);
            if (existente is null)
                db.Configuracion.Add(new Configuracion { Clave = clave, Valor = valor });
            else
                existente.Valor = valor;
            await db.SaveChangesAsync(ct);
        };

        await guardar(ConfiguracionService.ClavePasswordMinLongitud, request.LongitudMinima.ToString());
        await guardar(ConfiguracionService.ClavePasswordHistorial, request.Historial.ToString());
        await guardar(ConfiguracionService.ClavePasswordDuracionDias, request.DuracionDias.ToString());

        RegistrarAuditLog(usuarioId, "PasswordPolicyChanged", detalle: $"Política actualizada: longitudMin={request.LongitudMinima}, historial={request.Historial}, duracionDias={request.DuracionDias}.");
        return new(true, "Política de contraseña guardada.");
    }

    public async Task<CambioPasswordResult> DesactivacionDosFaAsync(int usuarioId, DesactivarDosFaRequest request, CancellationToken ct = default)
    {
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is null) return new(false, "No se encontró la cuenta.");

        var esPropia = ctx.Establecida && ctx.UsuarioId == usuarioId;
        if (!esPropia && (!ctx.Establecida || ctx.Rol != "Admin"))
            return new(false, "Solo el administrador puede desactivar el 2FA de otros usuarios.");

        if (!u.DosFaActivo)
            return new(false, "El 2FA no está habilitado en esta cuenta.");

        if (!string.IsNullOrEmpty(u.DosFaSecret))
        {
            var ok = await ValidarCodigo2FAAsync(u, request.CodigoActual ?? "", ct);
            if (!ok)
                return new(false, "El código de verificación actual no es válido.");
        }

        u.DosFaActivo = false;
        u.DosFaSecret = null;
        db.Update(u);
        await db.SaveChangesAsync(ct);

        RegistrarAuditLog(usuarioId, "DosFaDeactivate", detalle: "2FA deshabilitado");
        return new(true, "2FA deshabilitado correctamente.");
    }

    public async Task<DosFaEstadoDto> ObtenerEstadoDosFaAsync(int usuarioId, CancellationToken ct = default)
    {
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is null) return new(false, Mensaje: "No se encontró la cuenta.");

        var esPropia = ctx.Establecida && ctx.UsuarioId == usuarioId;
        if (!esPropia && (!ctx.Establecida || ctx.Rol != "Admin"))
            return new(false, Mensaje: "Solo el administrador puede consultar el estado del 2FA de otros usuarios.");

        // Sin 2FA activo y sin secreto pendiente: se genera para que el usuario pueda escanear el QR.
        if (!u.DosFaActivo && string.IsNullOrEmpty(u.DosFaSecret))
        {
            u.DosFaSecret = GenerarSecretarioTotp();
            db.Update(u);
            await db.SaveChangesAsync(ct);
        }
        var qrUrl = GenerarUrlQr(u);
        return new(true, UrlQr: qrUrl, ClaveSecreta: u.DosFaActivo ? null : u.DosFaSecret, DisponibleParaActivacion: !u.DosFaActivo);
    }

    public async Task<CambioPasswordResult> ActivacionDosFaAsync(int usuarioId, ActivarDosFaRequest request, CancellationToken ct = default)
    {
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is null) return new(false, "No se encontró la cuenta.");

        var esPropia = ctx.Establecida && ctx.UsuarioId == usuarioId;
        var esAdmin = ctx.Establecida && ctx.Rol == "Admin";
        if (!esPropia && !esAdmin)
            return new(false, "Solo el administrador puede activar el 2FA de otros usuarios.");
        if (string.IsNullOrWhiteSpace(u.DosFaSecret))
            return new(false, "Primero consulta el estado del 2FA para generar el código QR.");

        var ok = await ValidarCodigo2FAAsync(u, request.CodigoVerificacion ?? "", ct);
        if (!ok)
        {
            RegistrarAuditLog(usuarioId, "DosFaActivateFallido", detalle: "Código de verificación inválido al activar 2FA.");
            return new(false, "El código de verificación no es válido. Escanea el QR de nuevo y captura el código actual.");
        }

        u.DosFaActivo = true;
        db.Update(u);
        await db.SaveChangesAsync(ct);

        var qrUrl = GenerarUrlQr(u);
        var qrBase64 = GenerarQrImagenBase64(qrUrl);
        RegistrarAuditLog(usuarioId, "DosFaActivate", detalle: "2FA habilitado por " + (esPropia ? "el propio usuario" : "administrador"));
        return new(true, "2FA habilitado. Escanee el código QR con su aplicación de autenticación.", QrBase64: qrBase64, UrlQr: qrUrl);
    }

    public async Task<CambioPasswordResult> CambiarContraseñaAsync(int usuarioId, string contraseñaActual, CambioPasswordRequest request, CancellationToken ct = default)
    {
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is null) return new(false, "No se encontró la cuenta.");

        var esPropia = !ctx.Establecida || ctx.UsuarioId == usuarioId; // Sin sesión (login), la verificación de la contraseña actual acredita al dueño.
        var esAdmin = ctx.Establecida && ctx.Rol == "Admin";
        if (!esPropia && !esAdmin)
            return new(false, "Solo el administrador puede cambiar la contraseña de otros usuarios.");

        // El propietario debe conocer su contraseña actual; el administrador puede restablecerla.
        if (esPropia && !await VerificarConUpgradeAsync(u, contraseñaActual, ct))
            return new(false, "La contraseña actual es incorrecta.");

        var cfg = await ObtenerPasswordConfigAsync(ct);
        var validacion = ValidarComplejidad(request.NuevaContraseña, cfg);
        if (!validacion.IsValid)
            return new(false, validacion.Mensaje);

        var historial = await db.PasswordHistories
            .Where(h => h.UsuarioId == usuarioId)
            .OrderByDescending(h => h.FechaCambioUtc)
            .Take(cfg.Historial)
            .Select(h => h.PasswordHash)
            .ToListAsync(ct);

        var nuevoHash = Hasher.HashPassword(u, request.NuevaContraseña);
        // Los hashes de Identity llevan sal aleatoria: la comparación debe verificar la contraseña
        // contra cada hash del historial, no comparar cadenas.
        foreach (var hashPrevio in historial)
        {
            if (Hasher.VerifyHashedPassword(u, hashPrevio, request.NuevaContraseña) != PasswordVerificationResult.Failed)
                return new(false, $"La contraseña no puede repetirse con ninguna de las últimas {cfg.Historial} contraseñas.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            u.PasswordHash = nuevoHash;
            u.UltimoCambioPasswordUtc = DateTime.UtcNow;
            db.Update(u);

            var viejos = await db.PasswordHistories
                .Where(h => h.UsuarioId == usuarioId)
                .OrderBy(h => h.FechaCambioUtc)
                .Take(Math.Max(0, historial.Count + 1 - cfg.Historial))
                .ToListAsync(ct);
            if (viejos.Any()) db.PasswordHistories.RemoveRange(viejos);

            db.PasswordHistories.Add(new PasswordHistory
            {
                UsuarioId = usuarioId,
                PasswordHash = nuevoHash,
                FechaCambioUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            RegistrarAuditLog(usuarioId, "PasswordChange", detalle: $"Cambio de contraseña exitoso; no se repite con el historial de {cfg.Historial} contraseñas.");
            return new(true, "Contraseña actualizada correctamente.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex, "Error cambiando la contraseña del usuario {UsuarioId}.", usuarioId);
            return new(false, "Error al actualizar la contraseña. Intenta de nuevo.");
        }
    }

    private void RegistrarAuditLog(int usuarioId, string tipo, string? detalle = null, string? anterior = null, string? nuevo = null)
    {
        // La tabla exige un usuario real (FK). Sin sesión (p. ej. validación RFC previa al login)
        // o con un id inexistente se omite la fila para no romper el flujo principal.
        try
        {
            var u = usuarioId > 0 ? db.Usuarios.Find(usuarioId) : null;
            if (u is null)
            {
                logger.LogWarning("AuditLog omitido ({Tipo}): no hay usuario válido (id {UsuarioId}).", tipo, usuarioId);
                return;
            }
            db.AuditLogs.Add(new AuditLog
            {
                UsuarioId = usuarioId,
                UsuarioNombre = u.Nombre,
                Tipo = tipo,
                Detalle = detalle,
                Anterior = anterior,
                Nuevo = nuevo,
                FechaUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo escribir el AuditLog ({Tipo}) para el usuario {UsuarioId}.", tipo, usuarioId);
        }
    }
}

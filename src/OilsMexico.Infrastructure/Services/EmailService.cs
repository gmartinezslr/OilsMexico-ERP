using System.Net;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Envío de correos por SMTP (MailKit) para clientes y proveedores: cotizaciones, CFDI
/// timbrados, estados de cuenta, comunicados y recordatorios de saldo.
///
/// Configuración (patrón tabla <c>configuracion</c> con respaldo en appsettings <c>Correo</c>):
/// el <b>remitente</b> (correo de origen) y el servidor SMTP se capturan en la pantalla
/// Configuración; nada de esto está hardcodeado. Ver claves <c>Clave*</c> de esta clase.
/// </summary>
public sealed partial class EmailService(
    ErpDbContext db, IConfiguration cfg, ISucursalContext ctx,
    IEstadoCuentasService estadoCuentas, IBusquedaGlobalService plantillas) : IEmailService
{
    // Claves de la tabla configuracion (mismo patrón que ConfiguracionService: constantes públicas).
    public const string ClaveHabilitado = "correo_habilitado";
    public const string ClaveHost = "correo_smtp_host";
    public const string ClavePuerto = "correo_smtp_puerto";
    public const string ClaveSsl = "correo_smtp_ssl";
    public const string ClaveUsuario = "correo_smtp_usuario";
    public const string ClavePassword = "correo_smtp_password";
    public const string ClaveRemitente = "correo_remitente";
    public const string ClaveRemitenteNombre = "correo_remitente_nombre";
    public const string ClaveAutoCfdi = "correo_auto_cfdi";

    private const int PuertoPorDefecto = 587;
    private const string EmpresaEstatica = "OilsMexico";

    // ------------------------------------------------------------------
    // Configuración
    // ------------------------------------------------------------------

    public async Task<ConfigCorreoDto> ObtenerConfigAsync(CancellationToken ct = default)
    {
        // Una sola consulta de las 9 claves (la tabla es pares clave/valor).
        var valores = await db.Configuracion.AsNoTracking()
            .Where(c => c.Clave == ClaveHabilitado || c.Clave == ClaveHost || c.Clave == ClavePuerto
                || c.Clave == ClaveSsl || c.Clave == ClaveUsuario || c.Clave == ClavePassword
                || c.Clave == ClaveRemitente || c.Clave == ClaveRemitenteNombre || c.Clave == ClaveAutoCfdi)
            .ToDictionaryAsync(c => c.Clave, c => c.Valor, ct);

        // La BD manda si tiene valor; si no, appsettings (sección Correo); si tampoco, el defecto.
        string Texto(string claveAppsettings, string claveBd, string defecto)
            => !string.IsNullOrWhiteSpace(valores.GetValueOrDefault(claveBd))
                ? valores[claveBd].Trim()
                : (string.IsNullOrWhiteSpace(cfg[claveAppsettings]) ? defecto : cfg[claveAppsettings]!.Trim());

        return new ConfigCorreoDto(
            Texto("Correo:Habilitado", ClaveHabilitado, "0") is "1" or "true",
            Texto("Correo:Host", ClaveHost, ""),
            int.TryParse(Texto("Correo:Puerto", ClavePuerto, PuertoPorDefecto.ToString()), out var puerto)
                ? Math.Clamp(puerto, 1, 65535) : PuertoPorDefecto,
            Texto("Correo:Ssl", ClaveSsl, "1") is not ("0" or "false"),
            Texto("Correo:Usuario", ClaveUsuario, ""),
            Texto("Correo:Password", ClavePassword, ""),
            Texto("Correo:Remitente", ClaveRemitente, ""),
            Texto("Correo:RemitenteNombre", ClaveRemitenteNombre, "OilsMexico ERP"),
            Texto("Correo:AutoCfdi", ClaveAutoCfdi, "0") is "1" or "true");
    }


    public async Task<ConfigCorreoDto> GuardarConfigAsync(ConfigCorreoDto config, CancellationToken ct = default)
    {
        var host = (config.Host ?? "").Trim();
        var remitente = (config.Remitente ?? "").Trim();
        var puerto = Math.Clamp(config.Puerto, 1, 65535);
        var nombreRemitente = string.IsNullOrWhiteSpace(config.RemitenteNombre) ? "OilsMexico ERP" : config.RemitenteNombre.Trim();

        if (config.Habilitado)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Activaste el correo pero dejaste el servidor (Host) vacío.");
            if (!MailboxAddress.TryParse(remitente, out _))
                throw new InvalidOperationException(
                    $"'{remitente}' no es un correo válido. Captura el remitente (correo de origen) en formato usuario@dominio.com.");
        }

        await GuardarTextoAsync(ClaveHabilitado, config.Habilitado ? "1" : "0", ct);
        await GuardarTextoAsync(ClaveHost, host, ct);
        await GuardarTextoAsync(ClavePuerto, puerto.ToString(), ct);
        await GuardarTextoAsync(ClaveSsl, config.Ssl ? "1" : "0", ct);
        await GuardarTextoAsync(ClaveUsuario, (config.Usuario ?? "").Trim(), ct);
        await GuardarTextoAsync(ClavePassword, config.Password ?? "", ct);
        await GuardarTextoAsync(ClaveRemitente, remitente, ct);
        await GuardarTextoAsync(ClaveRemitenteNombre, nombreRemitente, ct);
        await GuardarTextoAsync(ClaveAutoCfdi, config.EnvioAutomaticoCfdi ? "1" : "0", ct);
        return await ObtenerConfigAsync(ct);
    }

    private async Task GuardarTextoAsync(string clave, string valor, CancellationToken ct)
    {
        var existente = await db.Configuracion.FirstOrDefaultAsync(c => c.Clave == clave, ct);
        if (existente is null) db.Configuracion.Add(new Configuracion { Clave = clave, Valor = valor });
        else existente.Valor = valor;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------
    // Transporte (MailKit)
    // ------------------------------------------------------------------

    public async Task<EnvioCorreoResult> EnviarAsync(EmailMensajeDto mensaje, CancellationToken ct = default)
    {
        var config = await ObtenerConfigAsync(ct);
        ValidarListoParaEnviar(config);
        await EnviarInternoAsync(config, mensaje, ct);
        return new EnvioCorreoResult(true, $"Correo enviado a {mensaje.Destinatario}.");
    }

    private void ValidarListoParaEnviar(ConfigCorreoDto config)
    {
        if (!config.Habilitado)
            throw new InvalidOperationException(
                "El envío de correos está deshabilitado. Actívalo en Configuración → Correo saliente (SMTP).");
        if (string.IsNullOrWhiteSpace(config.Host))
            throw new InvalidOperationException("Falta el servidor SMTP: captúralo en Configuración → Correo saliente.");
        if (!MailboxAddress.TryParse(config.Remitente, out _))
            throw new InvalidOperationException(
                "El remitente (correo de origen) no está configurado o no es válido. Captúralo en Configuración → Correo saliente.");
    }

    private static async Task EnviarInternoAsync(ConfigCorreoDto config, EmailMensajeDto mensaje, CancellationToken ct)
    {
        var destino = (mensaje.Destinatario ?? "").Trim();
        if (!MailboxAddress.TryParse(destino, out var destinoMail))
            throw new InvalidOperationException(
                $"El destinatario '{destino}' no es un correo válido. Corrige el correo del contacto y reintenta.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(config.RemitenteNombre) ? EmpresaEstatica : config.RemitenteNombre,
            config.Remitente));
        message.To.Add(destinoMail);
        message.Subject = mensaje.Asunto ?? "(sin asunto)";

        var body = new BodyBuilder { HtmlBody = mensaje.CuerpoHtml };
        if (mensaje.Adjuntos is not null)
            foreach (var adj in mensaje.Adjuntos)
                body.Attachments.Add(adj.NombreArchivo, adj.Contenido, ContentType.Parse(adj.MimeTipo));
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var socket = config.Ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
            await client.ConnectAsync(config.Host, config.Puerto, socket, ct);
            if (!string.IsNullOrWhiteSpace(config.Usuario))
                await client.AuthenticateAsync(config.Usuario, config.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo enviar el correo vía {config.Host}:{config.Puerto}: {ex.Message}", ex);
        }
    }

    // ------------------------------------------------------------------
    // Helpers de contenido (usados por EmailService.Documentos / .Masivos)
    // ------------------------------------------------------------------

    /// <summary>Nombre comercial para las plantillas: razón social de la sucursal de sesión (fallback "OilsMexico").</summary>
    private async Task<string> EmpresaAsync(CancellationToken ct)
    {
        var nombre = await db.Sucursales.AsNoTracking()
            .Where(s => s.Id == ctx.SucursalId)
            .Select(s => s.RazonSocial ?? s.Nombre)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(nombre) ? EmpresaEstatica : nombre;
    }

    private string Empresa() => db.Sucursales.AsNoTracking()
        .Where(s => s.Id == ctx.SucursalId)
        .Select(s => s.RazonSocial ?? s.Nombre)
        .FirstOrDefault() ?? EmpresaEstatica;

    /// <summary>Plantilla HTML con cabecera del ERP y pie de fecha (el cuerpo ya viene codificado en HTML).</summary>
    internal static string PlantillaHtml(string titulo, string cuerpoHtml)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#212529;margin:0;padding:16px;\">");
        sb.Append("<div style=\"max-width:680px;margin:0 auto;border:1px solid #dee2e6;border-radius:8px;overflow:hidden;\">");
        sb.Append("<div style=\"background:#212529;color:#fff;padding:12px 16px;\"><strong>")
          .Append(WebUtility.HtmlEncode(titulo)).Append("</strong></div>");
        sb.Append("<div style=\"padding:16px;\">").Append(cuerpoHtml).Append("</div>");
        sb.Append("<div style=\"background:#f8f9fa;color:#6c757d;padding:8px 16px;font-size:12px;\">")
          .Append(EmpresaEstatica).Append(" · Generado el ").Append(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
          .Append("</div></div></body></html>");
        return sb.ToString();
    }

    /// <summary>Tabla HTML; las celdas vienen ya codificadas en HTML por el llamador.</summary>
    internal static string TablaHtml(string[] encabezados, IEnumerable<string[]> filas)
    {
        var sb = new StringBuilder();
        sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;font-size:14px;\">");
        sb.Append("<tr style=\"background:#f1f3f5;\">");
        foreach (var h in encabezados)
            sb.Append("<th style=\"border:1px solid #dee2e6;text-align:left;\">").Append(WebUtility.HtmlEncode(h)).Append("</th>");
        sb.Append("</tr>");
        foreach (var fila in filas)
        {
            sb.Append("<tr>");
            foreach (var celda in fila)
                sb.Append("<td style=\"border:1px solid #dee2e6;\">").Append(celda).Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>Destino efectivo: el indicado o el del contacto; lanza si no hay ninguno válido.</summary>
    internal static string ResolverDestino(string? indicado, string? contactoEmail, string queEs)
    {
        var destino = string.IsNullOrWhiteSpace(indicado) ? (contactoEmail ?? "").Trim() : indicado.Trim();
        if (!MailboxAddress.TryParse(destino, out _))
            throw new InvalidOperationException(
                $"{queEs} no tiene correo registrado. Captúralo en la ficha del contacto o escribe un correo válido antes de enviar.");
        return destino;
    }

    public async Task<EnvioCorreoResult> ProbarAsync(string? destino = null, CancellationToken ct = default)
    {
        var config = await ObtenerConfigAsync(ct);
        ValidarListoParaEnviar(config);
        var para = string.IsNullOrWhiteSpace(destino) ? config.Remitente : destino.Trim();
        var html = PlantillaHtml("Prueba de configuración de correo",
            $"<p>Este correo de prueba confirma que el ERP <strong>{WebUtility.HtmlEncode(Empresa())}</strong> envía desde " +
            $"<strong>{WebUtility.HtmlEncode(config.Remitente)}</strong> vía {WebUtility.HtmlEncode(config.Host)}:{config.Puerto}.</p>");
        await EnviarInternoAsync(config, new EmailMensajeDto(para,
            $"[{Empresa()}] Prueba de correo SMTP", html), ct);
        return new EnvioCorreoResult(true, $"Correo de prueba enviado a {para} desde {config.Remitente}.");
    }
}

namespace OilsMexico.Application.DTOs;

/// <summary>
/// Configuración del correo saliente del ERP (SMTP). Se persiste en la tabla
/// <c>configuracion</c> (claves <c>correo_*</c>) con respaldo en appsettings (sección <c>Correo</c>).
/// <see cref="Remitente"/> es el correo de origen que ven los clientes/proveedores: es
/// configurable desde la pantalla Configuración (no está hardcodeado).
/// </summary>
public sealed record ConfigCorreoDto(
    bool Habilitado, string Host, int Puerto, bool Ssl,
    string Usuario, string Password,
    string Remitente, string RemitenteNombre, bool EnvioAutomaticoCfdi);

/// <summary>Adjunto binario de un correo (p. ej. el XML de un CFDI).</summary>
public sealed record EmailAdjuntoDto(string NombreArchivo, byte[] Contenido, string MimeTipo);

/// <summary>Correo a enviar: destinatario único, asunto, cuerpo HTML y adjuntos opcionales.</summary>
public sealed record EmailMensajeDto(
    string Destinatario, string Asunto, string CuerpoHtml,
    string? DestinatarioNombre = null, List<EmailAdjuntoDto>? Adjuntos = null);

/// <summary>Resultado de un envío individual de correo.</summary>
public sealed record EnvioCorreoResult(bool Exitoso, string Mensaje);

/// <summary>Solicitud de comunicado masivo a clientes o proveedores.</summary>
public sealed record ComunicadoRequest(
    string TipoDestinatario, string Asunto, string Cuerpo,
    bool SoloActivos = true, bool SoloConSaldo = false)
{
    public const string Clientes = "clientes";
    public const string Proveedores = "proveedores";
}

/// <summary>Resultado de un envío masivo (comunicados o recordatorios de saldo).</summary>
public sealed record ResultadoEnvioMasivoDto(int Enviados, int Fallidos, int SinCorreo, List<string> Errores);
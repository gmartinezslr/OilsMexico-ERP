using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Envío de correos del ERP por SMTP (MailKit). El servidor y el <b>remitente</b> se
/// configuran en la pantalla Configuración (tabla <c>configuracion</c>, claves <c>correo_*</c>,
/// con respaldo en appsettings sección <c>Correo</c>). Un único servicio da soporte a:
/// cotizaciones, CFDI timbrados (automático tras timbrar o manual), estados de cuenta de
/// clientes y proveedores, comunicados masivos y recordatorios de saldo.
/// Los errores de configuración/datos se lanzan como <see cref="InvalidOperationException"/>
/// con mensaje en español (patrón de los demás servicios); los envíos masivos no lanzan:
/// devuelven el desglose de resultados en <see cref="ResultadoEnvioMasivoDto"/>.
/// </summary>
public interface IEmailService
{
    // ---- configuración SMTP (remitente incluido) ----

    /// <summary>Lee la configuración vigente (tabla configuracion → appsettings → defectos).</summary>
    Task<ConfigCorreoDto> ObtenerConfigAsync(CancellationToken ct = default);

    /// <summary>Guarda la configuración SMTP (valida host/remitente cuando está habilitado).</summary>
    Task<ConfigCorreoDto> GuardarConfigAsync(ConfigCorreoDto config, CancellationToken ct = default);

    /// <summary>Envía un correo de prueba para verificar el servidor y el remitente. Destino por defecto: el remitente configurado.</summary>
    Task<EnvioCorreoResult> ProbarAsync(string? destino = null, CancellationToken ct = default);

    // ---- envío genérico ----

    /// <summary>Envía un mensaje armado por el llamador (asunto/cuerpo HTML/adjuntos).</summary>
    Task<EnvioCorreoResult> EnviarAsync(EmailMensajeDto mensaje, CancellationToken ct = default);

    // ---- documentos del ERP ----

    /// <summary>Envía la cotización (HTML con renglones y totales) al cliente. Destino por defecto: <c>Cliente.Email</c>.</summary>
    Task<EnvioCorreoResult> EnviarCotizacionAsync(int cotizacionId, string? correoDestino = null, CancellationToken ct = default);

    /// <summary>Envía el CFDI timbrado con el XML adjunto. Destino por defecto: <c>Cliente.Email</c>.</summary>
    Task<EnvioCorreoResult> EnviarCfdiAsync(int facturaId, string? correoDestino = null, CancellationToken ct = default);

    /// <summary>
    /// Hook del timbrado: envía el CFDI solo si el envío automático está activo
    /// (clave <c>correo_auto_cfdi</c>) y el cliente tiene correo. Devuelve null si no aplicó.
    /// Cualquier fallo de correo se traga para no romper el timbrado.
    /// </summary>
    Task<EnvioCorreoResult?> EnviarCfdiAutomaticoAsync(int facturaId, CancellationToken ct = default);

    /// <summary>Envía el estado de cuenta (facturas abiertas, pagos y saldo) a un cliente. Destino por defecto: <c>Cliente.Email</c>.</summary>
    Task<EnvioCorreoResult> EnviarEstadoCuentaClienteAsync(int clienteId, string? correoDestino = null, CancellationToken ct = default);

    /// <summary>Envía el estado de cuenta de CxP (compras abiertas, pagos y saldo) a un proveedor. Destino por defecto: <c>Proveedor.Email</c>.</summary>
    Task<EnvioCorreoResult> EnviarEstadoCuentaProveedorAsync(int proveedorId, string? correoDestino = null, CancellationToken ct = default);

    // ---- envíos masivos ----

    /// <summary>Comunicado masivo a clientes o proveedores (variables {Nombre}, {Fecha}, {Empresa}… rellenadas por destinatario).</summary>
    Task<ResultadoEnvioMasivoDto> EnviarComunicadoAsync(ComunicadoRequest request, CancellationToken ct = default);

    /// <summary>Recordatorio de saldo a todos los clientes con facturas abiertas de la sucursal de sesión (plantilla de cobro).</summary>
    Task<ResultadoEnvioMasivoDto> EnviarRecordatoriosSaldoAsync(string? asunto = null, string? cuerpo = null, CancellationToken ct = default);
}

using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

// ---------------------------------------------------------------------------
// 1) Cambio de contraseña
// ---------------------------------------------------------------------------

public sealed record CambioPasswordRequest(
    string NuevaContraseña,
    string? ConfirmarContraseña = null);

public sealed record CambioPasswordResult(
    bool Exitoso,
    string? Mensaje = null,
    string? TipoEvento = null,
    string? QrBase64 = null,
    string? UrlQr = null,
    string? ClaveSecreta = null);

// ---------------------------------------------------------------------------
// 2) 2FA (autenticación de dos factores) — habilitación/deshabilitación opcional
// ---------------------------------------------------------------------------

public sealed record DosFaEstadoDto(
    bool Habilitado,
    string? UrlQr = null,
    string? QrBase64 = null,
    string? ClaveSecreta = null,
    bool DisponibleParaActivacion = false,
    string? Mensaje = null);

public sealed record ActivarDosFaRequest(string CodigoVerificacion);

public sealed record DesactivarDosFaRequest(string? CodigoActual = null);

// ---------------------------------------------------------------------------
// 3) Política de contraseña (configurada por el administrador)
// ---------------------------------------------------------------------------

public sealed record PasswordConfigDto(
    int LongitudMinima,
    int Historial,
    int DuracionDias,
    bool RequiereMayusculas,
    bool RequiereMinusculas,
    bool RequiereNumeros,
    bool RequiereEspecial);

public sealed record PasswordConfigRequest(
    int LongitudMinima,
    int Historial,
    int DuracionDias,
    bool RequiereMayusculas,
    bool RequiereMinusculas,
    bool RequiereNumeros,
    bool RequiereEspecial);

public sealed record PasswordHistorialRejected(
    string Mensaje,
    string? UltimoHashUsado = null);

// ---------------------------------------------------------------------------
// 4) Validación RFC (estructura + semántica)
// ---------------------------------------------------------------------------

public sealed record ConsultaRfcResult(
    bool Valido,
    string? Mensaje,
    string? Tipo,            // "Fisica" | "Moral" | null
    int? Año,
    int Mes,
    int Dia,
    string? Clave,
    string? LetraVerificacion,
    bool RfcEnConformidadFiscal);

public sealed record ConsultaRfcRequest(string Rfc);

// ---------------------------------------------------------------------------
// 5) Política de bloqueo por intentos de acceso (configurada por el administrador)
// ---------------------------------------------------------------------------

/// <summary>
/// Parámetros del bloqueo tras intentos de login fallidos (antes hardcodeados en AuthService:
/// 3 intentos → bloqueo de 30 min; 2.º bloqueo → definitivo). Persistidos en la tabla
/// <c>configuracion</c> vía <c>IConfiguracionService</c>.
/// </summary>
public sealed record PoliticaBloqueoDto(
    int MaxIntentos,
    int MinutosBloqueo,
    int BloqueosParaDefinitivo);

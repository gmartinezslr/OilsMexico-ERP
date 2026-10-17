namespace OilsMexico.Application.DTOs;

/// <summary>Aviso interno (campana). FechaUtc para ordenar; Leido para el badge.</summary>
public sealed record AvisoDto(int Id, string Titulo, string? Detalle, string Tipo, DateTime FechaUtc, bool Leido);

/// <summary>Resultado de la búsqueda global (Ctrl+K): un renglón navegable por módulo.</summary>
public sealed record BusquedaGlobalDto(
    string Modulo,
    string Titulo,
    string? Subtitulo,
    string Url,
    string? Etiqueta = null,
    int? ClienteId = null,
    CreditoClienteDto? Credito = null);

/// <summary>Plantilla de mensaje operativo (WhatsApp / impresión / correo).</summary>
public sealed record PlantillaMensajeDto(
    string Clave,
    string Nombre,
    string Canal,
    string Cuerpo);

namespace OilsMexico.Application.DTOs;

/// <summary>Aviso interno (campana). FechaUtc para ordenar; Leido para el badge.</summary>
public sealed record AvisoDto(int Id, string Titulo, string? Detalle, string Tipo, DateTime FechaUtc, bool Leido);

namespace OilsMexico.Application.DTOs;

/// <summary>Opción de asentamiento devuelta al buscar por CP (para autocompletar colonia/municipio/estado).</summary>
public sealed record AsentamientoDto(
    string CodigoPostal,
    string Asentamiento,
    string TipoAsentamiento,
    string Municipio,
    string Estado,
    string? Ciudad,
    string? Zona);

/// <summary>Dirección resuelta por CP: cuántos asentamientos hay y valores comunes.</summary>
public sealed record DireccionSepomexDto(
    string CodigoPostal,
    string Municipio,
    string Estado,
    string? Ciudad,
    List<AsentamientoDto> Asentamientos);

public sealed record SepomexImportResult(
    int FilasLeidas,
    int Insertadas,
    int OmitidasDuplicadas,
    int Reemplazadas,
    string Modo);

public sealed record SepomexStatsDto(
    long TotalRegistros,
    int CodigosPostalesDistintos,
    int EstadosDistintos,
    DateTime? UltimaCarga);

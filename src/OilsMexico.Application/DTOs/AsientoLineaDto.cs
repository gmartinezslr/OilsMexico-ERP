namespace OilsMexico.Application.DTOs;

/// <summary>Línea resuelta de un asiento contable, listo para registrarse.</summary>
public sealed class AsientoLinea
{
    public string CuentaCodigo { get; init; } = string.Empty;
    public bool Debe { get; init; }
    public decimal Importe { get; init; }
}
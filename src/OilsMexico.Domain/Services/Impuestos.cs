namespace OilsMexico.Domain.Services;

/// <summary>
/// Fuente única de la tasa de IVA y de sus cálculos. Reemplaza los literales 0.16m / 1.16m
/// y las constantes TasaIva duplicadas en servicios y páginas: si la tasa cambia (o se
/// soportan tasas diferenciales), se toca un solo lugar.
/// </summary>
public static class Impuestos
{
    /// <summary>Tasa de IVA general (México, 16%).</summary>
    public const decimal TasaIva = 0.16m;

    /// <summary>Tasa en formato SAT (atributo TasaOCuota del nodo Traslado, 6 decimales).</summary>
    public const string TasaIvaSat = "0.160000";

    /// <summary>IVA resultante de una base gravable (importe sin IVA).</summary>
    public static decimal IvaDeBase(decimal baseGravable) => Math.Round(baseGravable * TasaIva, 2);

    /// <summary>Base (sin IVA) contenida en un importe que ya incluye IVA.</summary>
    public static decimal BaseDeTotal(decimal importeConIva) => Math.Round(importeConIva / (1m + TasaIva), 2);

    /// <summary>IVA contenido en un importe que ya lo incluye (total − base).</summary>
    public static decimal IvaDeTotal(decimal importeConIva) => Math.Round(importeConIva - BaseDeTotal(importeConIva), 2);
}
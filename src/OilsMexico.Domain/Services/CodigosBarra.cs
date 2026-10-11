namespace OilsMexico.Domain.Services;

/// <summary>
/// Fuente única de normalización y validación de códigos de barras
/// (presentaciones de producto, <c>unidades_medida.codigo_barra</c>).
/// Los escáneres USB actúan como teclado: envían dígitos + Enter, a veces con
/// espacios o guiones según la etiqueta. Por eso se normaliza antes de buscar
/// o de validar duplicados: trim + mayúsculas + quitar espacios/guiones internos.
/// </summary>
public static class CodigosBarra
{
    public const int LongitudMaxima = 50;

    /// <summary>Normaliza lo que captura el escáner o el usuario (null si vacío).</summary>
    public static string? Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var limpio = new string(codigo.Trim().ToUpperInvariant()
            .Where(c => c != ' ' && c != '-' && c != '.').ToArray());
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }

    /// <summary>
    /// ¿Parece un escaneo (solo dígitos, 8–14)? Se usa en el POS para decidir
    /// si se intenta alta directa al carrito en lugar de búsqueda por texto.
    /// </summary>
    public static bool PareceEscaneo(string? texto)
    {
        var n = Normalizar(texto);
        return n is not null && n.Length is >= 8 and <= 14 && n.All(char.IsDigit);
    }

    /// <summary>Valida formato antes de guardar. Solo caracteres de etiqueta + longitud.</summary>
    public static (bool Valido, string Mensaje) Validar(string? codigo)
    {
        var n = Normalizar(codigo);
        if (n is null) return (true, "Sin código (opcional).");
        if (n.Length > LongitudMaxima)
            return (false, $"El código de barras supera {LongitudMaxima} caracteres.");
        if (!n.All(c => char.IsLetterOrDigit(c)))
            return (false, "Solo letras y dígitos (sin símbolos). Se ignoran espacios y guiones.");
        // Aviso útil sin bloquear: los EAN/UPC comerciales miden 8, 12, 13 o 14 dígitos.
        return (true, "ok");
    }
}

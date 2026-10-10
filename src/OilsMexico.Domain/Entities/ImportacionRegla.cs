namespace OilsMexico.Domain.Entities;

/// <summary>
/// Regla de mapeo para importar productos desde una fuente externa.
/// Compara <c>patron</c> (Regex) sobre algún campo del archivo y asigna <c>valor</c>
/// al campo objetivo identificado por <c>tipo</c>. Las reglas se procesan por
/// <c>prioridad</c> ascendente y solo las activas <c>Activo = true</c> se aplican.
/// </summary>
public sealed class ImportacionRegla
{
    public int Id { get; set; }
    /// <summary>Tipo de campo o acción a aplicar (p. ej. Codigo, Nombre, Familia, Zona).</summary>
    public string Tipo { get; set; } = string.Empty;
    /// <summary>Patrón Regex que identifica los registros a los que aplica la regla.</summary>
    public string Patron { get; set; } = string.Empty;
    /// <summary>Valor asignado cuando el patrón coincide (p. ej. una familia, una zona, un código).</summary>
    public string Valor { get; set; } = string.Empty;
    public int Prioridad { get; set; } = 0; // menor valor = mayor prioridad
    public bool Activo { get; set; } = true;
}
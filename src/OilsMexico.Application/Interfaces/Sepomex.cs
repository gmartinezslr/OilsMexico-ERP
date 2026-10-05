using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Catálogo SEPOMEX: búsqueda por CP para autocompletar alta/modificación
/// de clientes y proveedores + carga masiva del TXT/CSV oficial.
/// </summary>
public interface ISepomexService
{
    /// <summary>Busca asentamientos por CP de 5 dígitos (ej: "20000").</summary>
    Task<DireccionSepomexDto?> BuscarPorCpAsync(string codigoPostal, CancellationToken ct = default);

    /// <summary>Utilería de búsqueda de colonias por nombre (el formulario usa el combo poblado por CP).</summary>
    Task<List<AsentamientoDto>> BuscarColoniasAsync(string texto, int limite = 20, CancellationToken ct = default);

    /// <summary>
    /// Importa el archivo oficial SEPOMEX: TXT delimitado por '|', CSV con comas
    /// o el Excel (.xls/.xlsx) que distribuye Correos de México (una hoja por estado).
    /// Acepta stream del IBrowserFile. reemplazar=true borra la tabla antes (TRUNCATE lógico).
    /// </summary>
    Task<SepomexImportResult> ImportarAsync(Stream archivo, bool reemplazar, CancellationToken ct = default);

    Task<SepomexStatsDto> EstadisticasAsync(CancellationToken ct = default);
}

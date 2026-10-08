using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>Servicio para procesar e importar listas de precios de aceites desde archivos Excel.</summary>
public interface IImportacionProductosService
{
    /// <summary>Lee y valida las filas del Excel sin modificar la base de datos (previsualización).</summary>
    Task<List<ProductoImportadoFilaDto>> PrevisualizarExcelAsync(Stream stream, CancellationToken ct = default);

    /// <summary>Importa o actualiza productos en el catálogo a partir del flujo del archivo Excel.</summary>
    Task<ImportacionProductosResumenDto> ImportarDesdeStreamAsync(Stream stream, bool actualizarExistentes = true, CancellationToken ct = default);

    /// <summary>Importa o actualiza productos a partir de un archivo en el servidor local (ej: lista_precios_aceites.xlsx).</summary>
    Task<ImportacionProductosResumenDto> ImportarDesdeArchivoLocalAsync(string rutaArchivo, bool actualizarExistentes = true, CancellationToken ct = default);
}

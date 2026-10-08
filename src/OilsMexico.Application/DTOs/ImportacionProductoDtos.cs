namespace OilsMexico.Application.DTOs;

/// <summary>Representa una fila leída de la lista de precios Excel para previsualización o importación.</summary>
public sealed class ProductoImportadoFilaDto
{
    public int FilaNumero { get; set; }
    public string Hoja { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string? SkuAnterior { get; set; }
    public string SkuActual { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Sae { get; set; } = string.Empty;
    public string Especificacion { get; set; } = string.Empty;
    public int PiezasPorCaja { get; set; } = 1;
    public decimal PrecioLp { get; set; }
    public decimal LpOroConIva { get; set; }
    public decimal PrecioUnitario { get; set; }

    public bool ExisteEnBd { get; set; }
    public int? ProductoExistenteId { get; set; }
    public bool EsValido { get; set; } = true;
    public string? ErrorValidacion { get; set; }
}

/// <summary>Resumen detallado del resultado del proceso de importación.</summary>
public sealed class ImportacionProductosResumenDto
{
    public int TotalFilas { get; set; }
    public int Insertados { get; set; }
    public int Actualizados { get; set; }
    public int Omitidos { get; set; }
    public int Errores { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public List<string> ErroresDetalle { get; set; } = [];
    public List<ProductoImportadoFilaDto> FilasProcesadas { get; set; } = [];
}

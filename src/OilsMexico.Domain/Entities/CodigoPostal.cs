namespace OilsMexico.Domain.Entities;

/// <summary>
/// Catálogo nacional SEPOMEX de códigos postales.
/// Tabla: codigos_postales. Equivale al DDL SQL Server [dbo].[CodigosPostales]
/// que muestras en la imagen, adaptado a snake_case de este ERP (PostgreSQL).
/// Fuente: https://www.correosdemexico.gob.mx/SSLServicios/ConsultaCP/CodigoPostal_Exportar.aspx
/// Formato TXT oficial: texto plano delimitado por '|' (también aceptamos ',' ';' TAB).
/// Orden de columnas del archivo: d_codigo|d_asenta|d_tipo_asenta|D_mnpio|d_estado|d_ciudad|d_CP|c_estado|c_oficina|c_CP|c_tipo_asenta|c_mnpio|id_asenta_cpcons|d_zona|c_cve_ciudad
/// </summary>
public sealed class CodigoPostal
{
    public int Id { get; set; }

    /// <summary>d_codigo — CP de 5 dígitos (ej: 20000). Clave de búsqueda.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>d_asenta — Nombre del asentamiento (ej: Aguascalientes Centro).</summary>
    public string Asentamiento { get; set; } = string.Empty;

    /// <summary>d_tipo_asenta — Colonia, Fraccionamiento, Barrio, etc.</summary>
    public string TipoAsentamiento { get; set; } = string.Empty;

    /// <summary>D_mnpio — Municipio / Alcaldía.</summary>
    public string Municipio { get; set; } = string.Empty;

    /// <summary>d_estado — Estado.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>d_ciudad — Ciudad (puede venir vacía).</summary>
    public string? Ciudad { get; set; }

    /// <summary>d_CP — Nombre de la oficina administradora (en tu imagen columna dCP).</summary>
    public string? Dcp { get; set; }

    /// <summary>c_estado — Clave estado (ej: 01).</summary>
    public string? EstadoId { get; set; }

    /// <summary>c_oficina — Clave oficina (ej: 20001).</summary>
    public string? Oficina { get; set; }

    /// <summary>c_CP — Vacío / no usado por SEPOMEX.</summary>
    public string? Ccp { get; set; }

    /// <summary>c_tipo_asenta — Clave tipo asentamiento (ej: 09, 21).</summary>
    public string? TipoAsentamientoId { get; set; }

    /// <summary>c_mnpio — Clave municipio (ej: 001).</summary>
    public string? MunicipioId { get; set; }

    /// <summary>id_asenta_cpcons — Consecutivo del asentamiento (ej: 0001).</summary>
    public string? AsentamientoId { get; set; }

    /// <summary>d_zona — Urbano / Rural.</summary>
    public string? Zona { get; set; }

    /// <summary>c_cve_ciudad — Clave ciudad (ej: 01).</summary>
    public string? CiudadId { get; set; }
}

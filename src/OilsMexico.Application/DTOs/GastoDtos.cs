namespace OilsMexico.Application.DTOs;

// ---------- Gastos operativos (/gastos) ----------

/// <summary>Renglón del listado de gastos (filtrable por fechas y categoría).</summary>
public sealed record GastoListadoDto(
    int GastoId, DateTime Fecha, string Categoria, string Concepto,
    decimal Importe, string FormaPagoSat, string? Comprobante,
    string? Notas, int SucursalId, int UsuarioId, string Usuario);

/// <summary>Alta/edición de un gasto operativo.</summary>
public sealed record CrearGastoRequest(
    int SucursalId, DateTime Fecha, string Categoria, string Concepto,
    decimal Importe, string FormaPagoSat, string? Comprobante, string? Notas);

/// <summary>Resultado de crear/editar/eliminar un gasto.</summary>
public sealed record GastoResult(
    int GastoId, DateTime Fecha, string Categoria, string Concepto,
    decimal Importe, string FormaPagoSat, string? Comprobante, string Estado);

/// <summary>Total acumulado por categoría (reporte por período/sucursal).</summary>
public sealed record GastoTotalCategoriaDto(string Categoria, decimal Total, int Movimientos);

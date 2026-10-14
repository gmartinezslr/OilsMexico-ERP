using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Exportación Excel/PDF (checklist §2.1): genera archivos .xlsx (ClosedXML) y .pdf
/// (QuestPDF) en memoria para descarga directa. Todo respeta REGLA #1 (sucursal).
/// </summary>
public interface IExportacionService
{
    /// <summary>Estado de cuenta de un cliente (facturas + pagos + saldo).</summary>
    Task<ArchivoExportadoDto> EstadoCuentaClienteAsync(int clienteId, CancellationToken ct = default);

    /// <summary>Cuentas por cobrar pendientes de la sucursal.</summary>
    Task<ArchivoExportadoDto> CxPendientesAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>Cuentas por pagar pendientes de la sucursal.</summary>
    Task<ArchivoExportadoDto> CxPPendientesAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>Inventario (stock por sucursal).</summary>
    Task<ArchivoExportadoDto> InventarioAsync(int sucursalId, string? filtro = null, CancellationToken ct = default);

    /// <summary>Kardex de un producto (movimientos).</summary>
    Task<ArchivoExportadoDto> KardexAsync(int sucursalId, int productoId, int dias = 30, CancellationToken ct = default);

    /// <summary>Ventas del periodo (historial completo sin paginar).</summary>
    Task<ArchivoExportadoDto> VentasAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Comisiones: cobranza efectiva del periodo por vendedor.</summary>
    Task<ArchivoExportadoDto> ComisionesAsync(int? sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Lista de precios del catálogo activo.</summary>
    Task<ArchivoExportadoDto> ListaPreciosAsync(CancellationToken ct = default);

    /// <summary>Gastos operativos del periodo por sucursal.</summary>
    Task<ArchivoExportadoDto> GastosAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>PDF de factura CFDI con QR SAT (representación impresa formal).</summary>
    Task<ArchivoExportadoDto> FacturaCfdiPdfAsync(int facturaId, CancellationToken ct = default);

    /// <summary>PDF/impresión de cotización (renglones + totales + vigencia).</summary>
    Task<ArchivoExportadoDto> CotizacionPdfAsync(int cotizacionId, CancellationToken ct = default);

    /// <summary>PDF/impresión de orden de venta.</summary>
    Task<ArchivoExportadoDto> OrdenVentaPdfAsync(int ordenId, CancellationToken ct = default);

    /// <summary>PDF de un estado financiero (balanza, balance, resultados, existencias).</summary>
    Task<ArchivoExportadoDto> FinancieroPdfAsync(string reporte, int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
}

/// <summary>
/// Reportes financieros (checklist §1.4): Balance General, Estado de Resultados por
/// sucursal, Balanza de Comprobación, cambio de existencias e indicadores.
/// Fuente: CuentasContables / AsientoContable / DetalleAsiento existentes.
/// </summary>
public interface IReportesFinancierosService
{
    /// <summary>Balanza de comprobación (debe/créditos del periodo por cuenta).</summary>
    Task<BalanzaComprobacionDto> BalanzaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Balance General al corte (Activo = Pasivo + Patrimonio).</summary>
    Task<BalanceGeneralDto> BalanceAsync(int sucursalId, DateTime corte, CancellationToken ct = default);

    /// <summary>Estado de Resultados por sucursal y periodo.</summary>
    Task<EstadoResultadosDto> ResultadosAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Cambio de existencias: inicial + compras − ventas ± ajustes ± traspasos.</summary>
    Task<CambioExistenciasDto> ExistenciasAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Indicadores por sucursal y periodo (margen, rotación, días de inventario).</summary>
    Task<List<IndicadoresSucursalDto>> IndicadoresAsync(DateTime desde, DateTime hasta, int? sucursalId = null, CancellationToken ct = default);
}

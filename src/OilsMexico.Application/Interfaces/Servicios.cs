using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>Proveedor de sucursal en sesión. REGLA INMUTABLE #1: todo se filtra por aquí.</summary>
public interface ISucursalContext
{
    int SucursalId { get; }
    int UsuarioId { get; }
    string Rol { get; }
    void Establecer(int sucursalId, int usuarioId, string rol);
}

public interface IVentasService
{
    Task<List<ProductoDto>> BuscarProductosAsync(int sucursalId, string? filtro, CancellationToken ct = default);
    Task<VentaPosResult> RegistrarVentaAsync(VentaPosRequest request, CancellationToken ct = default);
    Task<VentaPosResult> SurtirAsync(int facturaId, CancellationToken ct = default);
    Task<VentaPosResult> DevolverAsync(int facturaId, string motivo, CancellationToken ct = default);

    /// <summary>Historial paginado de ventas por sucursal con filtros de fecha/estado/texto y resumen.</summary>
    Task<HistorialResultadoDto> HistorialAsync(int sucursalId, DateTime desde, DateTime hasta,
        string? texto, string? estado, int pagina, CancellationToken ct = default);

    /// <summary>Detalle de una venta (renglones + datos CFDI). null si no existe o es de otra sucursal.</summary>
    Task<HistorialDetalleDto?> HistorialDetalleAsync(int facturaId, CancellationToken ct = default);
}

public interface IInventarioService
{
    Task EntradaCompraAsync(int sucursalId, int productoId, int? loteId, decimal litros, int usuarioId, string? motivo = null, CancellationToken ct = default);
    Task AjusteAsync(int sucursalId, int productoId, int? loteId, decimal stockRealLitros, int usuarioId, string motivo, CancellationToken ct = default);
    Task TraspasoAsync(int origenId, int destinoId, int productoId, decimal litros, int usuarioId, CancellationToken ct = default);
}

/// <summary>REGLA INMUTABLE #2: sellado nativo con System.Security.Cryptography (CSD .key/.cer, SHA-256).</summary>
public interface ICfdiSelladoService
{
    Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarAsync(int facturaId, CancellationToken ct = default);
}

/// <summary>Módulo Facturación CFDI: listado de folios, timbrado diferido, cancelación y descarga de XML.</summary>
public interface IFacturacionService
{
    Task<List<FacturaListadoDto>> ListarAsync(int sucursalId, string? estado, string? texto, CancellationToken ct = default);
    /// <summary>Sella y timbra una factura en estado Pendiente (timbrado diferido fuera del POS).</summary>
    Task<VentaPosResult> TimbrarAsync(int facturaId, CancellationToken ct = default);
    /// <summary>Cancela una factura ya timbrada. Motivo obligatorio (SAT). Solo modo SIMULADO por ahora.</summary>
    Task<VentaPosResult> CancelarAsync(int facturaId, string motivo, CancellationToken ct = default);
    /// <summary>Devuelve el XML sellado/timbrado o null si no existe.</summary>
    Task<string?> ObtenerXmlAsync(int facturaId, CancellationToken ct = default);
}

public interface IPacTimbradoService
{
    /// <summary>Timbra vía Web Service del PAC. En dev/staging puede operar en modo simulado.</summary>
    Task<Guid> TimbrarAsync(string xmlSellado, CancellationToken ct = default);
}

public interface ICatalogosSatService
{
    CatalogosSatDto Obtener();
}

/// <summary>Módulo Compras: orden → recepción (suma stock + kardex) → CxP (pagos).</summary>
public interface IComprasService
{
    Task<CompraResult> CrearOrdenAsync(CrearCompraRequest request, CancellationToken ct = default);
    Task<CompraResult> RecibirAsync(RecepcionRequest request, CancellationToken ct = default);
    Task<CompraResult> CancelarAsync(int compraId, string motivo, CancellationToken ct = default);
    Task<CompraResult> RegistrarPagoAsync(PagoCompraRequest request, CancellationToken ct = default);
    Task<List<CompraListadoDto>> ListarAsync(int sucursalId, string? estado, string? texto, CancellationToken ct = default);
    Task<CompraDetalleDto?> DetalleAsync(int compraId, CancellationToken ct = default);
    Task<List<CuentasPorPagarDto>> CuentasPorPagarAsync(int sucursalId, string? texto, CancellationToken ct = default);
}

/// <summary>Estado de cuenta de clientes / cuentas por cobrar (MVP).</summary>
public interface IEstadoCuentasService
{
    Task<List<EstadoCuentaClienteDto>> ListarEstadosCuentasAsync(int sucursalId, CancellationToken ct = default);
    Task<ClienteEstadoCuentaDto?> ObtenerEstadoCuentaAsync(int clienteId, CancellationToken ct = default);
}

/// <summary>Reportes de gestión (dashboard, margen/utilidad, rotación ABC, cortes por sucursal).</summary>
public interface IGestionService
{
    Task<DashboardGestionDto> DashboardAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    Task<List<VentasPorDiaDto>> VentasPorDiaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    Task<List<VentasPorVendedorDto>> VentasPorVendedorAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    Task<List<VentasPorProductoDto>> VentasPorProductoAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    Task<List<MarcaProductoDto>> VentasPorMarcaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    Task<List<RotacionABCDto>> RotacionAbcAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    Task<List<CorteSucursalDto>> CortesPorSucursalAsync(int sucursalId, CancellationToken ct = default);
    Task<List<ViscosidadDto>> InventarioPorViscosidadAsync(int sucursalId, CancellationToken ct = default);
}

/// <summary>Reglas de negocio para generar asientos contables a partir de transacciones.</summary>
public interface IAsientoGeneradorService
{
    Task<IReadOnlyList<(int tipo, decimal importe)>> GenerarAsientosDeVentaAsync(int sucursalId, int usuarioId, decimal total, decimal subtotal, decimal iva, string metodoPagoSat, string formaPagoSat);
    Task<IReadOnlyList<(int tipo, decimal importe)>> GenerarAsientosDeCompraAsync(int sucursalId, int usuarioId, decimal total);
    Task<IReadOnlyList<(int tipo, decimal importe)>> GenerarAsientosDeDevolucionAsync(int sucursalId, int usuarioId, decimal total);
}

/// <summary>Catálogo y gestión de la contabilidad básica (pólizas, libro mayor, saldos).</summary>
public interface ICuentasContablesService
{
    Task<List<CuentaContableDto>> ListarCuentasAsync(int sucursalId, CancellationToken ct = default);
    Task<CuentaContableDto?> ObtenerCuentaAsync(int cuentaId, CancellationToken ct = default);
    Task<RegistrarAsientoResult> RegistrarAsientoAsync(RegistrarAsientoRequest request, CancellationToken ct = default);
    Task<AsientoContableDto?> DetalleAsientoAsync(int asientoId, CancellationToken ct = default);
    Task<List<DetalleAsientoDto>> LibroMayorAsync(int sucursalId, DateTime desde, DateTime hasta, int? cuentaId, int pagina, CancellationToken ct = default);
    Task<List<CuentaContableDto>> SaldoPorCuentaAsync(int sucursalId, CancellationToken ct = default);
}

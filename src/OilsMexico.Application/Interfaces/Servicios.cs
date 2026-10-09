using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>Proveedor de sucursal en sesión. REGLA INMUTABLE #1: todo se filtra por aquí.</summary>
public interface ISucursalContext
{
    int SucursalId { get; }
    int UsuarioId { get; }
    string Rol { get; }

    /// <summary>
    /// ¿Hay una sesión REAL establecida? Los valores por defecto son «Admin, usuario 1, sucursal 1»
    /// para que el seed y el prerrender funcionen sin sesión, así que autorizar por <see cref="Rol"/>
    /// sin revisar esto sería FAIL-OPEN: cualquier ruta sin sesión heredaría privilegios de
    /// administrador. Toda comprobación de permisos debe empezar por aquí.
    /// </summary>
    bool Establecida { get; }

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
    /// <summary>Sella una Nota de Crédito CFDI 4.0 (Tipo E, CfdiRelacionados 01) con el CSD.</summary>
    Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarNotaCreditoAsync(int notaId, CancellationToken ct = default);
    /// <summary>Sella un Complemento de Pagos CFDI 4.0 (Tipo P + Pagos 2.0) con el CSD.</summary>
    Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarRepAsync(int repId, CancellationToken ct = default);
}

/// <summary>Módulo Facturación CFDI: listado de folios, timbrado diferido, cancelación y descarga de XML.</summary>
public interface IFacturacionService
{
    Task<List<FacturaListadoDto>> ListarAsync(int sucursalId, string? estado, string? texto, CancellationToken ct = default);
    /// <summary>Sella y timbra una factura en estado Pendiente (timbrado diferido fuera del POS).</summary>
    Task<VentaPosResult> TimbrarAsync(int facturaId, CancellationToken ct = default);
    /// <summary>Cancela una factura ya timbrada ante el PAC (cancel_signature). Motivo SAT 01|02|03|04 obligatorio; motivo 01 exige el UUID sustituto.</summary>
    Task<VentaPosResult> CancelarAsync(int facturaId, string motivo, string? folioSustitucion = null, CancellationToken ct = default);
    /// <summary>Devuelve el XML sellado/timbrado o null si no existe.</summary>
    Task<string?> ObtenerXmlAsync(int facturaId, CancellationToken ct = default);
}

public interface IPacTimbradoService
{
    /// <summary>
    /// Timbra vía Web Service del PAC (Finkok: SOAP stamp). SIMULADO devuelve un UUID local.
    /// Devuelve el UUID SAT y el XML timbrado (con el Timbre Fiscal Digital) para persistirlo.
    /// </summary>
    Task<(Guid Uuid, string XmlTimbrado)> TimbrarAsync(string xmlSellado, CancellationToken ct = default);
    /// <summary>
    /// Cancela un CFDI timbrado vía Finkok (cancel_signature): firma la solicitud con el CSD
    /// local (nunca viaja la llave al PAC) y envía Motivo SAT 01|02|03|04. SIMULADO no toca red.
    /// </summary>
    Task CancelarAsync(Guid uuid, string motivoSat, string? folioSustitucion = null, CancellationToken ct = default);
    /// <summary>Consulta el estado de un CFDI ante el SAT vía Finkok (get_sat_status).</summary>
    Task<string> EstadoSatAsync(Guid uuid, CancellationToken ct = default);
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

/// <summary>
/// Base de COBRANZA EFECTIVA (cash basis): el dinero que REALMENTE entró en un periodo,
/// atribuido al vendedor que vendió. Es la fuente de verdad del módulo de comisiones.
///
/// Reglas que implementa (fase 2 del módulo de comisiones):
///  - Fuente única: sólo se cuentan <c>venta_cobros</c>. Las ventas PUE (contado) generan un
///    cobro automático al registrarlas, así que ya no hace falta distinguir PUE de PPD.
///  - Prorrateo: un pago parcial acredita sólo la fracción cubierta de la factura; el resto
///    se libera cuando se cobre. La fracción se reparte por renglón para que el tabulador
///    por presentación pueda leer importes y litros por producto.
///  - Neto de IVA: la comisión se calcula sobre el subtotal prorrateado, no sobre el cobro
///    crudo (pagar comisión sobre IVA sería ~16% de más).
///  - Excluye facturas Canceladas y en Devolución aunque tengan cobros registrados.
/// </summary>
public interface ICobranzaService
{
    /// <summary>Cobranza efectiva agrupada por vendedor en el periodo. El vendedorId nulo agrupa como "(sin atribución)".</summary>
    Task<List<CobranzaResumenDto>> CobranzaPorVendedorAsync(int? sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Cobranza efectiva de UN vendedor concreto en el periodo.</summary>
    Task<CobranzaResumenDto> CobranzaVendedorAsync(int vendedorId, DateTime desde, DateTime hasta, int? sucursalId = null, CancellationToken ct = default);

    /// <summary>Detalle factura a factura (con renglones prorrateados) para auditoría y trazabilidad de comisiones.</summary>
    Task<List<CobranzaFacturaDto>> DetalleCobranzaAsync(int? vendedorId, DateTime desde, DateTime hasta, int? sucursalId = null, CancellationToken ct = default);

    /// <summary>
    /// Facturas emitidas por el vendedor en el periodo que siguen SIN cobrarse del todo, con la
    /// comisión que liberarían al cobrarse. Es la sección «Comisiones Potenciales» del portal:
    /// motiva la cobranza sin contar como ingresado (cash basis).
    /// </summary>
    Task<List<PotencialDto>> PotencialesAsync(int vendedorId, DateTime desde, DateTime hasta,
        int? sucursalId = null, CancellationToken ct = default);
}

/// <summary>
/// Módulo de comisiones: cuotas (metas), tabulador por presentación y cierre mensual congelado.
/// La base SIEMPRE es la cobranza efectiva de <see cref="ICobranzaService"/> (cash basis,
/// prorrateada y sin IVA); este servicio sólo aplica metas y porcentajes.
/// </summary>
public interface IComisionesService
{
    // ---- cuotas ----
    Task<List<CuotaDto>> ListarCuotasAsync(int? vendedorId, int? anio, int? mes, CancellationToken ct = default);
    Task<CuotaDto> GuardarCuotaAsync(GuardarCuotaRequest req, CancellationToken ct = default);
    Task EliminarCuotaAsync(int cuotaId, CancellationToken ct = default);

    // ---- tabulador por presentación ----
    Task<List<TabuladorDto>> ListarTabuladorAsync(int? productoId, CancellationToken ct = default);
    Task GuardarTabuladorAsync(GuardarTabuladorRequest req, CancellationToken ct = default);

    // ---- cálculo en vivo ----
    Task<ComisionCalculoDto> CalcularAsync(int vendedorId, int anio, int mes, int? sucursalId = null, CancellationToken ct = default);

    /// <summary>
    /// Avance del vendedor para el portal: cálculo en vivo, run-rate (proyección a fin de mes),
    /// potenciales por cobrar y si el periodo ya está congelado. Una sola llamada para la pantalla.
    /// </summary>
    Task<PortalVendedorDto> PortalAsync(int vendedorId, int anio, int mes, int? sucursalId = null, CancellationToken ct = default);

    // ---- cierre congelado ----
    Task<ComisionHistorialDto> CerrarPeriodoAsync(CerrarPeriodoRequest req, CancellationToken ct = default);
    Task<List<ComisionHistorialDto>> ListarHistorialAsync(int? vendedorId, int? anio, CancellationToken ct = default);
    /// <summary>Reabre un cierre (solo Admin): vuelve a Borrador para recalcularlo.</summary>
    Task ReabrirPeriodoAsync(int historialId, string motivo, CancellationToken ct = default);
    Task MarcarPagadoAsync(int historialId, CancellationToken ct = default);
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

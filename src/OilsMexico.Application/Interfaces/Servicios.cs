using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

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

    /// <summary>
    /// ¿La sesión vigente es de administrador? Centraliza la comparación con
    /// <see cref="Roles.Admin"/> (antes repetida como el literal "Admin" en decenas de sitios).
    /// Exige sesión real, por lo que es fail-closed.
    /// </summary>
    bool EsAdmin => Establecida && Rol == Roles.Admin;

    /// <summary>
    /// Resuelve la sucursal autorizada para una consulta: un Admin puede apuntar a la sucursal
    /// solicitada; cualquier otro rol queda fijado a su propia sucursal de sesión. Sustituye el
    /// patrón <c>Rol == "Admin" ? solicitada : SucursalId</c> que estaba duplicado en ~12 servicios
    /// (B8), evitando que el aislamiento multi-sucursal dependa de copiar bien la condición.
    /// </summary>
    int ResolverSucursalAutorizada(int sucursalSolicitada) => EsAdmin ? sucursalSolicitada : SucursalId;

    void Establecer(int sucursalId, int usuarioId, string rol);
}

/// <summary>
/// Faceta de POS/búsqueda de ventas (ISP): dependen de ella el POS y las cotizaciones al facturar.
/// </summary>
public interface IVentasPosService
{
    Task<List<ProductoDto>> BuscarProductosAsync(int sucursalId, string? filtro, CancellationToken ct = default);
    Task<VentaPosResult> RegistrarVentaAsync(VentaPosRequest request, CancellationToken ct = default);
}

/// <summary>Facturación transaccional de una orden de venta.</summary>
public interface IOrdenVentaFacturacionService
{
    Task<OrdenVentaFacturarResult> FacturarOrdenVentaAsync(
        int ordenId, OrdenVentaFacturarRequest request, CancellationToken ct = default);
}

/// <summary>Faceta de entrega en almacén (Surtir/Devolver) — Historial la consume para operar.</summary>
public interface IEntregaAlmacenService
{
    Task<VentaPosResult> SurtirAsync(int facturaId, CancellationToken ct = default);
    Task<VentaPosResult> DevolverAsync(int facturaId, string motivo, CancellationToken ct = default);
}

/// <summary>Faceta de consulta del historial de ventas (listado + detalle).</summary>
public interface IHistorialVentasService
{
    /// <summary>Historial paginado de ventas por sucursal con filtros de fecha/estado/texto y resumen.</summary>
    Task<HistorialResultadoDto> HistorialAsync(int sucursalId, DateTime desde, DateTime hasta,
        string? texto, EstadoFactura? estado, int pagina, CancellationToken ct = default);

    /// <summary>Detalle de una venta (renglones + datos CFDI). null si no existe o es de otra sucursal.</summary>
    Task<HistorialDetalleDto?> HistorialDetalleAsync(int facturaId, CancellationToken ct = default);
}

/// <summary>
/// Módulo de ventas completo. Compuesto de las facetas ISP <see cref="IVentasPosService"/>,
/// <see cref="IEntregaAlmacenService"/> e <see cref="IHistorialVentasService"/>: los consumidores
/// que sólo necesitan una capacidad deben depender de la faceta correspondiente.
/// </summary>
public interface IVentasService : IVentasPosService, IOrdenVentaFacturacionService,
    IEntregaAlmacenService, IHistorialVentasService
{
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
    Task<List<FacturaListadoDto>> ListarAsync(int sucursalId, EstadoFactura? estado, string? texto, CancellationToken ct = default);
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
    Task<CatalogosSatDto> ObtenerAsync(CancellationToken ct = default);
    string ObtenerGrupoFormaPago(string formaPagoSat);
    Task<string> ObtenerGrupoFormaPagoAsync(string formaPagoSat, CancellationToken ct = default);
}

/// <summary>Módulo Compras: orden → recepción (suma stock + kardex) → CxP (pagos).</summary>
public interface IComprasService
{
    Task<CompraResult> CrearOrdenAsync(CrearCompraRequest request, CancellationToken ct = default);
    Task<CompraResult> RecibirAsync(RecepcionRequest request, CancellationToken ct = default);
    Task<CompraResult> CancelarAsync(int compraId, string motivo, CancellationToken ct = default);
    Task<CompraResult> RegistrarPagoAsync(PagoCompraRequest request, CancellationToken ct = default);
    Task<List<CompraListadoDto>> ListarAsync(int sucursalId, EstadoCompra? estado, string? texto, CancellationToken ct = default);
    Task<CompraDetalleDto?> DetalleAsync(int compraId, CancellationToken ct = default);
    Task<List<CuentasPorPagarDto>> CuentasPorPagarAsync(int sucursalId, string? texto, CancellationToken ct = default);

    /// <summary>
    /// Antigüedad de saldos CxP: compras pendientes de pago agrupadas en rangos
    /// 0-15 / 16-30 / 31-60 / +60 días por proveedor.
    /// La fecha de vencimiento = FechaEmision + DiasCredito del proveedor (default 30 días si no aplica).
    /// Los intereses moratorios usan la tasa diaria configurada en <c>cxp_tasa_moratoria_diaria</c>.
    /// Default 0 = sin intereses. REGLA #1: sucursal resuelta con ResolverSucursalAutorizada.
    /// </summary>
    Task<AntiguedadSaldosCxpDto> AntiguedadSaldosCxpAsync(int sucursalId, CancellationToken ct = default);
}

/// <summary>
/// Conciliación bancaria. Cierra el hueco «los pagos/cobros se registran pero no la
/// operación bancaria»: aquí se captura el extracto del banco y se empata cada renglón
/// con el cobro (VentaCobro, Abono) o el pago a proveedor (CompraPago, Cargo) del ERP.
///
/// Reglas de conciliación:
///  - Un Abono (entró dinero) sólo se concilia contra un <see cref="CobroResult"/> del ERP
///    (VentaCobro); un Cargo (salió dinero) sólo contra un pago a proveedor (CompraPago).
///  - Cada movimiento del ERP y cada renglón del extracto se concilian una sola vez.
///  - El saldo de la cuenta se deriva del saldo inicial + Σ abonos − Σ cargos del extracto.
/// </summary>
public interface IBancosService
{
    // ---- cuentas bancarias ----
    Task<List<CuentaBancariaDto>> ListarCuentasAsync(int sucursalId, CancellationToken ct = default);
    Task<CuentaBancariaDto> CrearCuentaAsync(CrearCuentaBancariaRequest request, CancellationToken ct = default);

    // ---- extracto ----
    Task<List<MovimientoBancarioDto>> ListarMovimientosAsync(int cuentaId, EstadoConciliacion? estado, CancellationToken ct = default);
    Task<MovimientoBancarioDto> CargarMovimientoAsync(CargarMovimientoRequest request, CancellationToken ct = default);

    // ---- candidatos del ERP sin conciliar ----
    Task<List<MovimientoErpDto>> MovimientosErpPendientesAsync(int sucursalId, CancellationToken ct = default);

    // ---- conciliación ----
    Task ConciliarAsync(ConciliarMovimientoRequest request, CancellationToken ct = default);
    Task DesconciliarAsync(int movimientoId, CancellationToken ct = default);

    // ---- resumen ----
    Task<ResumenConciliacionDto> ResumenAsync(int cuentaId, CancellationToken ct = default);
}

/// <summary>
/// Gastos operativos de la distribuidora (gasolina, taller, servicios…).
/// Cierra el hueco «la caja no concilia con la operación real»: los gastos en
/// efectivo del turno entran al cuadre de caja como egresos (ver CorteCajaService)
/// y cada gasto genera su póliza (gasto vs. banco/efectivo) con ReglasAsiento.
/// REGLA #1: toda lectura/escritura se filtra por sucursal autorizada.
/// </summary>
public interface IGastoService
{
    Task<GastoResult> CrearAsync(CrearGastoRequest request, CancellationToken ct = default);
    Task<GastoResult> EditarAsync(int gastoId, CrearGastoRequest request, CancellationToken ct = default);
    Task EliminarAsync(int gastoId, CancellationToken ct = default);
    Task<List<GastoListadoDto>> ListarAsync(int sucursalId, DateTime desde, DateTime hasta, string? categoria, CancellationToken ct = default);
    /// <summary>Reporte: totales por categoría en el período/sucursal (para el informe de gastos).</summary>
    Task<List<GastoTotalCategoriaDto>> TotalesPorCategoriaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
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

    /// <summary>Saldo vencido actual del cliente (facturas sin pagar pendientes por vencer).</summary>
    Task<decimal> SaldoVencidoPorClienteAsync(int clienteId, CancellationToken ct = default);
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

    /// <summary>
    /// Antigüedad de saldos CxC: facturas vencidas agrupadas en rangos 0-15 / 16-30 / 31-60 / +60 días.
    /// La fecha de vencimiento se calcula como FechaEmision + DiasCredito del cliente (o 0 si es contado).
    /// Los intereses moratorios usan la tasa diaria configurada en <c>cxc_tasa_moratoria_diaria</c>
    /// (patrón igual que <c>password_policy_*</c>). Default 0 = sin intereses.
    /// REGLA #1: sucursal resuelta con ResolverSucursalAutorizada.
    /// </summary>
    Task<AntiguedadSaldosCxcDto> AntiguedadSaldosAsync(int sucursalId, CancellationToken ct = default);
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
    /// <summary>
    /// Líneas de venta vs. ingresos (evento "Venta" en ReglasAsiento).
    /// El usuario y la forma de pago se registran en el asiento al persistirlo
    /// (<see cref="RegistrarAsientoRequest"/>), por eso el generador sólo recibe lo que
    /// alimenta las reglas: sucursal (REGLA #1 al cargar cuentas) e importes.
    /// </summary>
    Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeVentaAsync(int sucursalId, decimal total, decimal subtotal, decimal iva);
    Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeCompraAsync(int sucursalId, decimal total);
    Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeDevolucionAsync(int sucursalId, decimal total);
    /// <summary>Líneas gasto vs. banco/efectivo (evento "Gasto" en ReglasAsiento).</summary>
    Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeGastoAsync(int sucursalId, decimal total);

    /// <summary>
    /// Costo de ventas FIFO del periodo (§1.3): reconstruye las capas de inventario desde el
    /// kardex (entradas COMPRA con el costo de compra por litro; respaldo: Producto.PrecioCosto)
    /// y suma el costo consumido por las salidas VENTA de facturas activas (misma regla de
    /// ingreso que el Estado de Resultados: no canceladas ni devueltas). REGLA #1 aplicada.
    /// </summary>
    Task<CostoVentasFifoDto> CalcularCostoVentasFifoAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>
    /// Líneas del asiento "Costo de Ventas vs. Almacén" del periodo (§1.3): evento "CostoVenta"
    /// en ReglasAsiento si está sembrado, o fallback 5000↔1200. Devuelve vacío si no hay costo.
    /// </summary>
    Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeCostoVentaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default);
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

/// <summary>
/// Cierre de periodo contable mensual (checklist §1.3): genera el asiento de costo de
/// ventas FIFO del mes, marca el periodo cerrado (bandera + bloqueo de escritura de
/// asientos en RegistrarAsientoAsync) y permite reabrirlo solo con rol Admin.
/// REGLA #1: sucursal resuelta con ResolverSucursalAutorizada.
/// </summary>
public interface ICierreContableService
{
    /// <summary>Periodos cerrados de la sucursal con su costo de ventas registrado.</summary>
    Task<List<PeriodoContableDto>> ListarAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>¿La fecha (mes local) cae en un periodo cerrado de la sucursal?</summary>
    Task<bool> EstaCerradoAsync(int sucursalId, DateTime fecha, CancellationToken ct = default);

    /// <summary>
    /// Cierra el periodo: siembra cuentas faltantes, registra el asiento de costo de ventas FIFO
    /// fechado al cierre del mes (si hay costo) y marca el periodo como cerrado. Admin/Conta.
    /// </summary>
    Task<CierreContableResultDto> CerrarAsync(int sucursalId, int anio, int mes, CancellationToken ct = default);

    /// <summary>Reabre el periodo: elimina el asiento de cierre, revierte sus saldos y la bandera. Solo Admin.</summary>
    Task ReabrirAsync(int sucursalId, int anio, int mes, CancellationToken ct = default);
}

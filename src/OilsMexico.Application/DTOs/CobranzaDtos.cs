namespace OilsMexico.Application.DTOs;

/// <summary>
/// Renglón de factura prorrateado al dinero que REALMENTE se cobró en el periodo.
/// <c>NetoSinIvaCobrado</c> es la base correcta de comisión (el importe incluye IVA;
/// el IVA no es ingreso y no debe generar comisión). <c>LitrosCobrados</c> aplica el
/// mismo porcentaje de cobertura que el dinero: un pago parcial no libera litros completos.
/// </summary>
public sealed record CobranzaRenglonDto(
    int ProductoId, string Producto, string Unidad,
    decimal Cantidad, decimal LitrosTotales, decimal ImporteConIva,
    decimal NetoSinIvaCobrado, decimal LitrosCobrados);

/// <summary>Factura que aportó cobro dentro del periodo (cash basis), con prorrateo ya aplicado.</summary>
public sealed record CobranzaFacturaDto(
    int FacturaId, string FolioInterno, DateTime FechaCobro,
    int ClienteId, string Cliente, int? VendedorId, string Vendedor,
    string MetodoPagoSat, int CobrosEnPeriodo,
    decimal TotalFactura, decimal NetoTotalSinIva,
    decimal CobradoEnPeriodo, decimal FraccionCobrada,
    decimal NetoCobradoSinIva, decimal LitrosCobrados,
    List<CobranzaRenglonDto> Renglones);

/// <summary>Resumen de cobranza efectiva de un vendedor en un periodo (base de comisiones).</summary>
public sealed record CobranzaResumenDto(
    int? VendedorId, string Vendedor,
    decimal CobradoBrutoConIva, decimal NetoCobradoSinIva, decimal LitrosCobrados,
    int FacturasCobradas, int CobrosAplicados);

/// <summary>
/// Factura emitida y AÚN no cobrada del todo: la «comisión potencial» del portal del vendedor.
/// Es motivacional y explícitamente NO es dinero ganado — falta el cobro (cash basis).
/// </summary>
public sealed record PotencialDto(
    int FacturaId, string FolioInterno, string Cliente, DateTime FechaEmision,
    decimal TotalConIva, decimal PendienteConIva, decimal NetoSinIvaPendiente,
    decimal LitrosPendientes, int DiasEmitida, decimal ComisionPotencial);

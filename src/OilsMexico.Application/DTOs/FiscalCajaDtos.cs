using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.DTOs;

// ---------- Notas de Crédito CFDI (Tipo E) ----------

/// <summary>Renglón de NC precargado desde la factura origen.</summary>
public sealed record NotaCreditoLineaDto(
    int ProductoId, string Producto, string Sku, string UnidadNombre,
    decimal Cantidad, decimal PrecioUnitario, decimal Importe, int? LoteId);

/// <summary>Preview de NC: factura origen + renglones sugeridos + resument fiscal.</summary>
public sealed record NotaCreditoPreviewDto(
    int FacturaId, string FolioOrigen, string Cliente, Guid? UuidOrigen,
    decimal TotalOrigen, EstadoFactura EstadoOrigen, bool TieneNcPrevia,
    List<NotaCreditoLineaDto> Lineas,
    decimal Subtotal, decimal Iva, decimal Total);

public sealed record CrearNotaCreditoRequest(
    int FacturaOrigenId, string Motivo, string? Observaciones);

public sealed record NotaCreditoResult(
    int NotaId, string FolioInterno, Guid? UuidSat,
    decimal Subtotal, decimal Iva, decimal Total, string Estado);

public sealed record NotaCreditoListadoDto(
    int NotaId, string FolioInterno, DateTime FechaEmision,
    string Cliente, string FolioOrigen, Guid? UuidSat,
    string Motivo, decimal Total, EstadoNotaCredito Estado, bool TieneXml);

// ---------- Complemento de pagos / REP (Tipo P) ----------

/// <summary>Venta PPD con saldo pendiente de cobro (cuentas por cobrar).</summary>
public sealed record VentaPpdPendienteDto(
    int FacturaId, string FolioInterno, DateTime FechaEmision, string Cliente,
    int ClienteId, decimal Total, decimal Cobrado, decimal Saldo, int Parcialidades);

public sealed record RegistrarCobroRequest(
    int FacturaId, decimal Monto, string FormaPagoSat, string? Referencia);

public sealed record CobroResult(
    int CobroId, int RepId, string FolioRep, decimal Monto,
    decimal SaldoInsoluto, string EstadoRep);

public sealed record RepListadoDto(
    int RepId, string FolioInterno, DateTime FechaEmision, string Cliente,
    decimal Total, Guid? UuidSat, string Estado, int Cobros, bool TieneXml);

public sealed record RepDetalleDto(
    int RepId, string FolioInterno, DateTime FechaEmision, string Cliente,
    decimal Total, Guid? UuidSat, string Estado,
    List<RepDoctoDto> Documentos);

public sealed record RepDoctoDto(
    string FolioFactura, Guid? UuidFactura, int NumParcialidad,
    decimal ImpSaldoAnt, decimal ImpPagado, decimal ImpSaldoInsoluto, string Moneda);

// ---------- Corte de caja / arqueo por turno ----------

public sealed record CorteAbiertoDto(
    int CorteId, int SucursalId, DateTime FechaAperturaUtc,
    string UsuarioApertura, decimal FondoInicial);

public sealed record CortePreviewDto(
    int CorteId, int NumVentas, decimal FondoInicial,
    decimal EfectivoSistema, decimal TarjetaSistema,
    decimal TransferSistema, decimal OtrosSistema, decimal TotalVentasSistema);

public sealed record CerrarCorteRequest(
    int CorteId, decimal EfectivoContado, string? Observaciones);

public sealed record CorteResult(
    int CorteId, DateTime? FechaCierreUtc, decimal TotalVentasSistema,
    decimal EfectivoContado, decimal Diferencia, string Estado);

public sealed record CorteListadoDto(
    int CorteId, DateTime FechaAperturaUtc, DateTime? FechaCierreUtc,
    string UsuarioApertura, decimal FondoInicial,
    decimal TotalVentasSistema, decimal EfectivoContado,
    decimal Diferencia, string Estado, int NumVentas);

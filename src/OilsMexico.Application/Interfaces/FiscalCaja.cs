using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Notas de Crédito CFDI 4.0 (TipoComprobante=E):
/// documento electrónico con UUID propio que referencia la factura origen (01).
/// </summary>
public interface INotaCreditoService
{
    /// <summary>Preview fiscal de la NC (renglones espejo + totales) sin persistir.</summary>
    Task<NotaCreditoPreviewDto> PreviewAsync(int facturaOrigenId, CancellationToken ct = default);
    /// <summary>Crea + sella (Tipo E real) + timbra la NC vía PAC y repone stock.</summary>
    Task<NotaCreditoResult> CrearYTimbrarAsync(CrearNotaCreditoRequest request, CancellationToken ct = default);
    Task<List<NotaCreditoListadoDto>> ListarAsync(int sucursalId, EstadoNotaCredito? estado, string? texto, CancellationToken ct = default);
    Task<string?> ObtenerXmlAsync(int notaId, CancellationToken ct = default);
    /// <summary>Cancela la NC ante el PAC (cancel_signature). Motivo SAT 01|02|03|04; 01 exige sustituto.</summary>
    Task<NotaCreditoResult> CancelarAsync(int notaId, string motivo, string? folioSustitucion = null, CancellationToken ct = default);
}

/// <summary>
/// Complemento de pagos / REP (TipoComprobante=P):
/// liquida formalmente ventas PPD con parcialidades y DoctoRelacionado.
/// </summary>
public interface IComplementoPagoService
{
    Task<List<VentaPpdPendienteDto>> PendientesAsync(int sucursalId, CancellationToken ct = default);
    /// <summary>Registra un cobro y lo ampara en un REP timbrado (1 cobro = 1 REP).</summary>
    Task<CobroResult> RegistrarCobroAsync(RegistrarCobroRequest request, CancellationToken ct = default);
    Task<List<RepListadoDto>> ListarAsync(int sucursalId, string? estado, CancellationToken ct = default);
    Task<RepDetalleDto?> DetalleAsync(int repId, CancellationToken ct = default);
    Task<string?> ObtenerXmlAsync(int repId, CancellationToken ct = default);
}

/// <summary>Corte / arqueo de caja por turno: apertura, cuadre y cierre.</summary>
public interface ICorteCajaService
{
    Task<CorteAbiertoDto?> AbiertoAsync(int sucursalId, CancellationToken ct = default);
    Task<CorteAbiertoDto> AbrirAsync(int sucursalId, decimal fondoInicial, CancellationToken ct = default);
    /// <summary>Foto del sistema al momento: ventas del turno por forma de pago.</summary>
    Task<CortePreviewDto> PreviewAsync(int corteId, CancellationToken ct = default);
    Task<CorteResult> CerrarAsync(CerrarCorteRequest request, CancellationToken ct = default);
    Task<List<CorteListadoDto>> HistorialAsync(int sucursalId, int dias, CancellationToken ct = default);
}

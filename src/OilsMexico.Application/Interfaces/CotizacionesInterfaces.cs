using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Application.Interfaces;

/// <summary>Cotizaciones y órdenes de venta (pre-ventas, compromiso de precio).</summary>
public interface ICotizacionesService
{
    /// <summary>Listado paginado de cotizaciones por sucursal.</summary>
    Task<List<CotizacionListadoDto>> ListarCotizacionesAsync(
        int sucursalId, EstadoCotizacion? estado, string? clienteTexto,
        DateTime? desde, DateTime? hasta, int pagina, int porPagina, CancellationToken ct = default);

    /// <summary>Cabecera + renglones de una cotización; null si no existe.</summary>
    Task<CotizacionResponseDto?> ObtenerCotizacionAsync(int id, CancellationToken ct = default);

    /// <summary>Alta de una nueva cotización.</summary>
    Task<CotizacionCrearResult> CrearCotizacionAsync(CotizacionCrearRequest req, CancellationToken ct = default);

    /// <summary>Actualiza los datos de una cotización abierta (estado Borrador/Cancelada).</summary>
    Task<CotizacionResponseDto?> ActualizarCotizacionAsync(int id, CotizacionCrearRequest req, CancellationToken ct = default);

    /// <summary>Convierte la cotización en una orden de venta (precio de compromiso, vendedor).</summary>
    Task<CotizacionConvertirResult> ConvertirCotizacionAsync(int id, CotizacionConvertirRequest req, CancellationToken ct = default);

    /// <summary>Anula una cotización (solo estado Borrador/Enviada/Cancelada).</summary>
    Task<OrdenResultado> CancelarCotizacionAsync(int id, string motivo, CancellationToken ct = default);

    /// <summary>Marca como Expirada las cotizaciones vencidas (debe llamarse periódicamente).</summary>
    Task<List<CotizacionListadoDto>> ExpirarCotizacionesAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>Listado paginado de órdenes de venta por sucursal.</summary>
    Task<List<OrdenVentaListadoDto>> ListarOrdenesVentaAsync(
        int sucursalId, EstadoOrdenVenta? estado, string? clienteTexto,
        DateTime? desde, DateTime? hasta, int pagina, int porPagina, CancellationToken ct = default);

    /// <summary>Detalle completo de una orden de venta; null si no existe.</summary>
    Task<OrdenVentaDetalleDto?> ObtenerOrdenVentaAsync(int id, CancellationToken ct = default);

    /// <summary>Alta de orden de venta manual (no derivada de cotización).</summary>
    Task<OrdenVentaCrearResult> CrearOrdenVentaAsync(OrdenVentaCrearRequest req, CancellationToken ct = default);

    /// <summary>Confirma una orden de venta: fija vendedor y pasa a Confirmada.</summary>
    Task<OrdenVentaConfirmarResult> ConfirmarOrdenVentaAsync(int id, OrdenVentaConfirmarRequest req, CancellationToken ct = default);

    /// <summary>Anula una orden de venta (solo estado Borrador/Confirmada/Suspendida).</summary>
    Task<OrdenResultado> CancelarOrdenVentaAsync(int id, string motivo, CancellationToken ct = default);

    // ---- catálogos para las pantallas de cotización / orden ----

    /// <summary>Productos activos de una sucursal (para el alta de cotización).</summary>
    Task<List<ProductoDto>> ListarProductosAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>Clientes activos de una sucursal.</summary>
    Task<List<(int Id, string Nombre)>> ListarClientesAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>Vendedores (usuarios con rol Vendedor) de una sucursal.</summary>
    Task<List<(int Id, string Nombre)>> ListarVendedoresAsync(int sucursalId, CancellationToken ct = default);
}

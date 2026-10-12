namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Lecturas del panel de inicio (Home) por rol. Existe para que la página no inyecte
/// <c>ErpDbContext</c> (DIP): la UI depende de esta abstracción de la capa Application y
/// no del ORM ni del ensamblado de Infrastructure.
/// </summary>
public interface IDashboardService
{
    /// <summary>Ventas del mes en curso de la sucursal y su serie diaria de los últimos 14 días.</summary>
    Task<DashboardVentasDto> VentasMesAsync(int sucursalId, DateTime hoyUtc, CancellationToken ct = default);

    /// <summary>Total de clientes activos y cuántos compraron en el mes indicado.</summary>
    Task<DashboardClientesDto> ClientesMesAsync(int sucursalId, DateTime inicioMesUtc, CancellationToken ct = default);

    /// <summary>
    /// Productos de la sucursal cuyo stock está en o bajo el mínimo, ordenados del más
    /// crítico al menos. Devuelve el total (la página decide cuántos mostrar).
    /// </summary>
    Task<List<DashboardStockBajoDto>> StockBajoAsync(int sucursalId, CancellationToken ct = default);

    /// <summary>Clientes activos más recientes (actividad para Vendedor/Admin).</summary>
    Task<List<DashboardActividadDto>> ClientesRecientesAsync(int max = 5, CancellationToken ct = default);

    /// <summary>Movimientos de almacén más recientes de la sucursal (actividad para Almacén).</summary>
    Task<List<DashboardActividadDto>> MovimientosRecientesAsync(int sucursalId, int max = 5, CancellationToken ct = default);

    /// <summary>Facturas más recientes no canceladas de la sucursal (actividad para Conta).</summary>
    Task<List<DashboardActividadDto>> FacturasRecientesAsync(int sucursalId, int max = 5, CancellationToken ct = default);
}

/// <summary>Total vendido en el mes y la serie diaria usada por la gráfica.</summary>
public sealed record DashboardVentasDto(decimal TotalMes, DateTime Desde, List<DashboardPuntoDto> Serie);

/// <summary>Total de ventas de un día de la serie.</summary>
public sealed record DashboardPuntoDto(DateTime Fecha, decimal Total);

/// <summary>Padrón de clientes contra los atendidos en el periodo.</summary>
public sealed record DashboardClientesDto(int Activos, int Atendidos, int PorAtender);

/// <summary>Producto bajo mínimo para la tarjeta del panel.</summary>
public sealed record DashboardStockBajoDto(string Nombre, decimal Stock, decimal Minimo);

/// <summary>Renglón de "actividad reciente" (neutral: vale para clientes, movimientos o facturas).</summary>
public sealed record DashboardActividadDto(string Titulo, string Subtitulo, string Detalle, string Color);

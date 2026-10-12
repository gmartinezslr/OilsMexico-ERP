using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Lecturas y CRUD liviano de catálogos maestros (productos, sucursales, unidades,
/// usuarios) que antes ejecutaba cada página Blazor inyectando <c>ErpDbContext</c> directo.
/// Al depender de esta abstracción, la capa web no conoce el ORM (DIP) y la lógica de
/// acceso a datos queda en un solo lugar por entidad (SRP).
/// </summary>
public interface ICatalogosService
{
    // ---- Productos ----
    Task<List<ProductoCatalogoDto>> BuscarProductosAsync(string? filtro, CancellationToken ct = default);
    Task<ProductoCatalogoDto?> ObtenerProductoAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje, int Id)> GuardarProductoAsync(ProductoCatalogoDto producto, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje)> CambiarEstadoProductoAsync(int id, bool activo, CancellationToken ct = default);

    // ---- Presentaciones (unidades de venta) ----
    Task<List<UnidadMedidaDto>> ListarUnidadesAsync(int productoId, CancellationToken ct = default);
    Task<UnidadMedidaDto?> ObtenerUnidadAsync(int id, CancellationToken ct = default);
    Task<List<StockSucursalDto>> StockPorSucursalAsync(int productoId, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje, int Id)> GuardarUnidadAsync(UnidadMedidaDto unidad, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje)> EliminarUnidadAsync(int id, CancellationToken ct = default);

    // ---- Sucursales ----
    Task<List<SucursalDto>> ListarSucursalesAsync(CancellationToken ct = default);
    Task<SucursalDto?> ObtenerSucursalAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje, int Id)> GuardarSucursalAsync(SucursalDto sucursal, CancellationToken ct = default);
    Task<List<SucursalResumenDto>> ListarSucursalesResumenAsync(CancellationToken ct = default);

    // ---- Usuarios ----
    Task<List<UsuarioRowDto>> ListarUsuariosAsync(string? filtro, bool soloActivos, CancellationToken ct = default);
    Task<UsuarioDto?> ObtenerUsuarioAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje, int Id)> GuardarUsuarioAsync(UsuarioDto usuario, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje)> CambiarEstadoUsuarioAsync(int id, bool activo, CancellationToken ct = default);
    Task<List<VendedorDto>> ListarVendedoresAsync(CancellationToken ct = default);
}

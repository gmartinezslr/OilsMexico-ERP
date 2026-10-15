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
    /// <summary>
    /// Listado de productos para la página Productos: filtro multi-campo (SKU, SKU anterior,
    /// nombre, marca, viscosidad, categoría, especificación), opcional de inactivos y límite.
    /// </summary>
    Task<List<ProductoCatalogoDto>> BuscarProductosAsync(
        string? filtro, bool soloActivos = true, int limite = 250, CancellationToken ct = default);
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
    /// <summary>
    /// Guarda (alta o edición) un usuario. Cuando <paramref name="passwordNueva"/> es verdadero
    /// el servicio registra el historial de contraseñas (recortándolo a <paramref name="historialContraseñas"/>),
    /// revoca sesiones abiertas y escribe la auditoría del cambio, en lugar de que esa escritura
    /// multi-tabla viva en la página (DIP + trazabilidad §3). <paramref name="actorNombre"/> identifica
    /// al administrador que hizo el cambio para el <c>AuditLog</c>.
    /// </summary>
    Task<(bool Ok, string Mensaje, int Id)> GuardarUsuarioAsync(
        UsuarioDto usuario, bool passwordNueva = false, string? actorNombre = null,
        int historialContraseñas = 3, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje)> CambiarEstadoUsuarioAsync(int id, bool activo, CancellationToken ct = default);
    Task<List<VendedorDto>> ListarVendedoresAsync(CancellationToken ct = default);
}

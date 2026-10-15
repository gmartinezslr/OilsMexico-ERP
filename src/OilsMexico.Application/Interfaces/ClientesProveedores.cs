using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// CRUD del padrón de clientes que antes ejecutaba la página Blazor inyectando
/// <c>ErpDbContext</c> directo (DIP §6.2). El servicio valida y persiste; las validaciones
/// con feedback en vivo (RFC del SAT, situación fiscal, domicilio SEPOMEX) siguen en la página.
/// </summary>
public interface IClientesService
{
    Task<List<ClienteCatalogoDto>> ListarAsync(string? filtro, bool soloActivos = true, CancellationToken ct = default);
    Task<ClienteCatalogoDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje, int Id)> GuardarAsync(ClienteCatalogoDto cliente, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje)> DesactivarAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// CRUD del padrón de proveedores que antes ejecutaba la página Blazor inyectando
/// <c>ErpDbContext</c> directo (DIP §6.2). Mismo reparto de responsabilidades que
/// <see cref="IClientesService"/>.
/// </summary>
public interface IProveedoresService
{
    Task<List<ProveedorCatalogoDto>> ListarAsync(string? filtro, bool soloActivos = true, CancellationToken ct = default);
    Task<ProveedorCatalogoDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje, int Id)> GuardarAsync(ProveedorCatalogoDto proveedor, CancellationToken ct = default);
    Task<(bool Ok, string Mensaje)> DesactivarAsync(int id, CancellationToken ct = default);
}
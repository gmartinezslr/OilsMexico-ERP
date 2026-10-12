namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Catálogos de solo lectura que las páginas Blazor consultan para llenar combos.
/// Sustituye la inyección directa de <c>ErpDbContext</c> en las vistas (DIP): la UI
/// depende de esta abstracción y de DTOs planos, nunca de las entidades del ORM.
/// </summary>
public interface ICatalogoConsultaService
{
    Task<List<OpcionDto>> SucursalesAsync(CancellationToken ct = default);

    Task<List<OpcionDto>> ClientesAsync(bool soloActivos = true, CancellationToken ct = default);

    Task<List<OpcionDto>> ProveedoresAsync(bool soloActivos = true, CancellationToken ct = default);

    /// <summary>Usuarios con rol Vendedor, para asignar el vendedor de una cuenta.</summary>
    Task<List<OpcionDto>> VendedoresAsync(CancellationToken ct = default);

    /// <summary>Mapa clienteId → vendedorId (quien lo dio de alta).</summary>
    Task<Dictionary<int, int>> VendedorPorClienteAsync(CancellationToken ct = default);

    /// <summary>Ids de todas las sucursales registradas.</summary>
    Task<List<int>> SucursalIdsAsync(CancellationToken ct = default);
}

/// <summary>Par id/nombre para llenar un <c>&lt;select&gt;</c>.</summary>
public sealed record OpcionDto(int Id, string Nombre);

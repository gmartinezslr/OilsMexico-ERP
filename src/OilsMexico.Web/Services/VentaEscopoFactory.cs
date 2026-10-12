using Microsoft.EntityFrameworkCore;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Web.Services;

/// <summary>
/// Fábrica de ámbitos (scopes) dueña de la creación del <see cref="ErpDbContext"/>, para que los
/// componentes Blazor dependan de una abstracción y no del DbContext concreto (DIP). Un
/// <see cref="IServiceScope"/> garantiza un contexto propio por operación: el servicio
/// <c>scoped</c> que resuelve dentro del ámbito usa ese mismo contexto, y al liberar el ámbito
/// se libera también el contexto sin contaminar el del circuito.
/// </summary>
public interface IVentaEscopoFactory
{
    Task<T> EjecutarAsync<T>(Func<ErpDbContext, Task<T>> operacion, CancellationToken ct = default);
    Task<List<T>> ListarAsync<T>(Func<ErpDbContext, IQueryable<T>> consulta, CancellationToken ct = default);
}

/// <summary>Implementación sobre <see cref="IServiceScopeFactory"/> (registrada como singleton).</summary>
public sealed class VentaEscopoFactory(IServiceScopeFactory scopes) : IVentaEscopoFactory
{
    public async Task<T> EjecutarAsync<T>(Func<ErpDbContext, Task<T>> operacion, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await operacion(db);
    }

    public async Task<List<T>> ListarAsync<T>(Func<ErpDbContext, IQueryable<T>> consulta, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await consulta(db).ToListAsync(ct);
    }
}

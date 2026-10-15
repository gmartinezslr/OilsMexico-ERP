using Microsoft.EntityFrameworkCore;
using OilsMexico.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace OilsMexico.IntegrationTests;

/// <summary>
/// Fixture compartido de PostgreSQL real vía Testcontainers (checklist §7, línea 98).
/// Levanta UN contenedor por clase de pruebas y expone la cadena de conexión.
/// Los tests InMemory no ejecutan el SQL real de PostgreSQL (CHECK, precisión numeric,
/// unique index, transacciones, conversiones enum↔texto), así que estas pruebas validan
/// el comportamiento del proveedor de BD de verdad.
/// </summary>
public sealed class PgFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_test")
        .WithUsername("erp")
        .WithPassword("erp_test_pw")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Crea un <see cref="ErpDbContext"/> contra el PostgreSQL real. Se replica el arranque de
    /// producción: <c>EnsureCreated</c> (crea el esquema desde el modelo EF) + <c>SchemaPatch</c>
    /// (columnas/tablas nuevas que EnsureCreated no altera) + <c>PeriodoContablePatch</c>.
    /// </summary>
    public async Task<(ErpDbContext db, string Connection)> NuevaDbAsync()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await SchemaPatch.AplicarAsync(db);
        await PeriodoContablePatch.AplicarAsync(db);
        return (db, ConnectionString);
    }
}

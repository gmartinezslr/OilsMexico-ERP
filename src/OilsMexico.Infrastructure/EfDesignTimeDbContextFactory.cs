using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure;

/// <summary>
/// Factory de diseño (design-time) para que las herramientas EF Core
/// (add-migration, update-database) creen instancias de <see cref="ErpDbContext"/>
/// en tiempo de diseño, sin necesidad de un servidor de base de datos real.
/// </summary>
public class EfDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ErpDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=OilsMexico;Username=postgres;Password=postgres");
        return new ErpDbContext(optionsBuilder.Options);
    }
}
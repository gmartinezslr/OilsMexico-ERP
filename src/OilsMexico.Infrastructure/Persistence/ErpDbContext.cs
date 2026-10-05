using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence;

public sealed class ErpDbContext(DbContextOptions<ErpDbContext> options) : DbContext(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<InventarioLote> Lotes => Set<InventarioLote>();
    public DbSet<InventarioSucursal> InventarioSucursal => Set<InventarioSucursal>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<FacturaDetalle> FacturaDetalles => Set<FacturaDetalle>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
    }
}

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
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<CodigoPostal> CodigosPostales => Set<CodigoPostal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraDetalle> CompraDetalles => Set<CompraDetalle>();
    public DbSet<CompraPago> CompraPagos => Set<CompraPago>();
    public DbSet<MovimientoInventario> Movimientos => Set<MovimientoInventario>();
    public DbSet<NotaCredito> NotasCredito => Set<NotaCredito>();
    public DbSet<NotaCreditoDetalle> NotaCreditoDetalles => Set<NotaCreditoDetalle>();
    public DbSet<VentaCobro> VentaCobros => Set<VentaCobro>();
    public DbSet<ComplementoPago> ComplementosPago => Set<ComplementoPago>();
    public DbSet<ComplementoPagoDetalle> ComplementoPagoDetalles => Set<ComplementoPagoDetalle>();
    public DbSet<CuentaContable> CuentasContables => Set<CuentaContable>();
    public DbSet<AsientoContable> AsientosContables => Set<AsientoContable>();
    public DbSet<DetalleAsiento> DetallesAsientos => Set<DetalleAsiento>();
    public DbSet<CorteCaja> CortesCaja => Set<CorteCaja>();
    public DbSet<Configuracion> Configuracion => Set<Configuracion>();


    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
    }
}

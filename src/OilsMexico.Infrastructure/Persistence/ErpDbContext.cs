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
    public DbSet<ContactoTercero> ContactosTercero => Set<ContactoTercero>();
    public DbSet<CorreoTercero> CorreosTercero => Set<CorreoTercero>();
    public DbSet<TelefonoTercero> TelefonosTercero => Set<TelefonoTercero>();
    public DbSet<DireccionTercero> DireccionesTercero => Set<DireccionTercero>();
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
    public DbSet<CuotaVendedor> CuotasVendedor => Set<CuotaVendedor>();
    public DbSet<ComisionHistorial> ComisionesHistorial => Set<ComisionHistorial>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    // ---------------------------------------------------------
    //  PUNTO 4: Roles y permisos
    // ---------------------------------------------------------
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();

    // ---------------------------------------------------------
    //  PUNTO 5: Reglas del plan contable
    // ---------------------------------------------------------
    public DbSet<ReglaAsiento> ReglasAsiento => Set<ReglaAsiento>();

    // ---------------------------------------------------------
    //  PUNTO 6: Reglas de importación de productos
    // ---------------------------------------------------------
    public DbSet<ImportacionParametro> ImportacionParametros => Set<ImportacionParametro>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<CotizacionDetalle> CotizacionDetalles => Set<CotizacionDetalle>();
    public DbSet<OrdenVenta> OrdenesVenta => Set<OrdenVenta>();
    public DbSet<OrdenVentaDetalle> OrdenVentaDetalles => Set<OrdenVentaDetalle>();

    public DbSet<ImportacionRegla> ImportacionReglas => Set<ImportacionRegla>();

    // ---------------------------------------------------------
    //  Conciliación bancaria (extracto del banco ↔ cobros/pagos)
    // ---------------------------------------------------------
    public DbSet<CuentaBancaria> CuentasBancarias => Set<CuentaBancaria>();
    public DbSet<MovimientoBancario> MovimientosBancarios => Set<MovimientoBancario>();

    // ---------------------------------------------------------
    //  Gastos operativos (/gastos): concilian la caja real
    // ---------------------------------------------------------
    public DbSet<Gasto> Gastos => Set<Gasto>();

    // ---------------------------------------------------------
    //  Cierre de periodo contable (§1.3): bandera de mes cerrado
    // ---------------------------------------------------------
    public DbSet<PeriodoContable> PeriodosContables => Set<PeriodoContable>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(ErpDbContext).Assembly);
    }
}

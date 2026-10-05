using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class LoteConfig : IEntityTypeConfiguration<InventarioLote>
{
    public void Configure(EntityTypeBuilder<InventarioLote> e)
    {
        e.ToTable("inventario_lotes");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.NumeroLote).HasColumnName("numero_lote").HasMaxLength(50).IsRequired();
        e.Property(x => x.FechaFabricacion).HasColumnName("fecha_fabricacion");
        e.Property(x => x.FechaCaducidad).HasColumnName("fecha_caducidad");
        e.Property(x => x.CantidadDisponible).HasColumnName("cantidad_disponible").HasPrecision(12, 2);
        e.Property(x => x.AlmacenId).HasColumnName("almacen_id");
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
    }
}

public sealed class InventarioSucursalConfig : IEntityTypeConfiguration<InventarioSucursal>
{
    public void Configure(EntityTypeBuilder<InventarioSucursal> e)
    {
        e.ToTable("inventario_sucursal");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.LoteId).HasColumnName("lote_id");
        e.Property(x => x.StockActual).HasColumnName("stock_actual").HasPrecision(12, 2);
        e.Property(x => x.StockMinimo).HasColumnName("stock_minimo").HasPrecision(12, 2).HasDefaultValue(5m);
        e.Property(x => x.ActualizadoUtc).HasColumnName("actualizado_utc");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Lote).WithMany().HasForeignKey(x => x.LoteId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.SucursalId, x.ProductoId, x.LoteId })
            .IsUnique().HasDatabaseName("uq_sucursal_producto_lote");
    }
}

public sealed class MovimientoConfig : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> e)
    {
        e.ToTable("movimientos_inventario");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.LoteId).HasColumnName("lote_id");
        e.Property(x => x.CantidadLitros).HasColumnName("cantidad_litros").HasPrecision(12, 2);
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20);
        e.Property(x => x.Motivo).HasColumnName("motivo");
        e.Property(x => x.ReferenciaId).HasColumnName("referencia_id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.FechaUtc).HasColumnName("fecha_utc");
    }
}

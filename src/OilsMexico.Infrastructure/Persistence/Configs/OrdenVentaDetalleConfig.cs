using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class OrdenVentaDetalleConfig : IEntityTypeConfiguration<OrdenVentaDetalle>
{
    public void Configure(EntityTypeBuilder<OrdenVentaDetalle> e)
    {
        e.ToTable("orden_venta_detalle");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.OrdenVentaId).HasColumnName("orden_venta_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.UnidadMedidaId).HasColumnName("unidad_medida_id");
        e.Property(x => x.UnidadNombre).HasColumnName("unidad_nombre").HasMaxLength(20);
        e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(12, 4);
        e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 4);
        e.Property(x => x.Descuento).HasColumnName("descuento").HasPrecision(12, 2);
        e.Property(x => x.Importe).HasColumnName("importe").HasPrecision(12, 2);
        e.Property(x => x.LoteId).HasColumnName("lote_id");
        e.HasOne(x => x.OrdenVenta).WithMany(o => o.Detalles).HasForeignKey(x => x.OrdenVentaId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
    }
}

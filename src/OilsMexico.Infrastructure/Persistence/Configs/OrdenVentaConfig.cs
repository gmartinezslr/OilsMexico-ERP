using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class OrdenVentaConfig : IEntityTypeConfiguration<OrdenVenta>
{
    public void Configure(EntityTypeBuilder<OrdenVenta> e)
    {
        e.ToTable("ordenes_venta");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.VendedorId).HasColumnName("vendedor_id");
        e.Property(x => x.FechaEmision).HasColumnName("fecha_emision");
        e.Property(x => x.FolioInterno).HasColumnName("folio_interno").HasMaxLength(40);
        e.Property(x => x.Notas).HasColumnName("notas").HasColumnType("text");
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.CotizacionId).HasColumnName("cotizacion_id");
        e.Property(x => x.FacturaId).HasColumnName("factura_id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        e.Property(x => x.Iva).HasColumnName("iva").HasPrecision(12, 2);
        e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.Cliente).WithMany(c => c.OrdenesVenta).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId);
        e.HasOne(x => x.Cotizacion).WithMany(c => c.OrdenesVenta).HasForeignKey(x => x.CotizacionId).OnDelete(DeleteBehavior.Restrict);
        // Factura generada al facturar la orden: opcional; al borrar la factura la orden vuelve a "sin facturar".
        e.HasOne(x => x.Factura).WithMany().HasForeignKey(x => x.FacturaId).OnDelete(DeleteBehavior.SetNull);
        e.HasMany(x => x.Detalles).WithOne(d => d.OrdenVenta).HasForeignKey(d => d.OrdenVentaId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.FacturaId).HasDatabaseName("IX_ordenes_venta_factura_id");
    }
}

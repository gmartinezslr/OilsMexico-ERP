using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CotizacionConfig : IEntityTypeConfiguration<Cotizacion>
{
    public void Configure(EntityTypeBuilder<Cotizacion> e)
    {
        e.ToTable("cotizaciones");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.VendedorId).HasColumnName("vendedor_id");
        e.Property(x => x.FechaEmision).HasColumnName("fecha_emision");
        e.Property(x => x.FechaVencimiento).HasColumnName("fecha_vencimiento");
        e.Property(x => x.FolioInterno).HasColumnName("folio_interno").HasMaxLength(40);
        e.Property(x => x.Notas).HasColumnName("notas").HasColumnType("text");
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        e.Property(x => x.Iva).HasColumnName("iva").HasPrecision(12, 2);
        e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.Cliente).WithMany(c => c.Cotizaciones).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId);
        e.HasMany(x => x.Detalles).WithOne(d => d.Cotizacion).HasForeignKey(d => d.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.OrdenesVenta).WithOne(o => o.Cotizacion).HasForeignKey(o => o.CotizacionId).OnDelete(DeleteBehavior.Restrict);
    }
}

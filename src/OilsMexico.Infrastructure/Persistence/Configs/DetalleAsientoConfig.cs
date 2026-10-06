using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class DetalleAsientoConfig : IEntityTypeConfiguration<DetalleAsiento>
{
    public void Configure(EntityTypeBuilder<DetalleAsiento> e)
    {
        e.ToTable("detalle_asientos");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.AsientoContableId).HasColumnName("asiento_contable_id");
        e.Property(x => x.CuentaId).HasColumnName("cuenta_id");
        e.Property(x => x.TipoMovimiento).HasColumnName("tipo_movimiento");
        e.Property(x => x.Importe).HasColumnName("importe").HasPrecision(12, 2);
        e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(250);
        e.HasOne(x => x.AsientoContable).WithMany(a => a.Detalles).HasForeignKey(x => x.AsientoContableId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Cuenta).WithMany().HasForeignKey(x => x.CuentaId).OnDelete(DeleteBehavior.Restrict);
    }
}

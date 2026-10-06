using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class AsientoContableConfig : IEntityTypeConfiguration<AsientoContable>
{
    public void Configure(EntityTypeBuilder<AsientoContable> e)
    {
        e.ToTable("asientos_contables");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.FechaUtc).HasColumnName("fecha_utc");
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(30);
        e.Property(x => x.Numeracion).HasColumnName("numeracion").HasMaxLength(30);
        e.Property(x => x.Concepto).HasColumnName("concepto").HasMaxLength(200);
        e.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(600);
        e.HasMany(x => x.Detalles).WithOne(d => d.AsientoContable).OnDelete(DeleteBehavior.Cascade);
    }
}

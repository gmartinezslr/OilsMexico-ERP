using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CatalogoSatConfig : IEntityTypeConfiguration<CatalogoSat>
{
    public void Configure(EntityTypeBuilder<CatalogoSat> e)
    {
        e.ToTable("catalogos_sat");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Catalogo).HasColumnName("catalogo").HasMaxLength(60).IsRequired();
        e.Property(x => x.Clave).HasColumnName("clave").HasMaxLength(20).IsRequired();
        e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(255).IsRequired();
        e.Property(x => x.Grupo).HasColumnName("grupo").HasMaxLength(50);
        e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        e.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0);

        e.HasIndex(x => new { x.Catalogo, x.Clave }).IsUnique();
        e.HasIndex(x => new { x.Catalogo, x.Activo });
    }
}

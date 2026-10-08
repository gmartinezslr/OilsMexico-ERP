using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class ConfiguracionConfig : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> e)
    {
        e.ToTable("configuracion");
        e.HasKey(x => x.Clave);
        e.Property(x => x.Clave).HasColumnName("clave").HasMaxLength(80).IsRequired();
        e.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(200).IsRequired();
    }
}

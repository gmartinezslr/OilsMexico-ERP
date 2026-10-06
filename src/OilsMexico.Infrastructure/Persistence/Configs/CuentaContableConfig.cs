using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CuentaContableConfig : IEntityTypeConfiguration<CuentaContable>
{
    public void Configure(EntityTypeBuilder<CuentaContable> e)
    {
        e.ToTable("cuentas_contables");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        e.Property(x => x.Tipo).HasColumnName("tipo");
        e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(250);
        e.Property(x => x.SaldoActual).HasColumnName("saldo_actual").HasPrecision(12, 2);
    }
}

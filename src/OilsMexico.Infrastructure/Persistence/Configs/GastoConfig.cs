using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

/// <summary>Config EF de <see cref="Gasto"/> (snake_case, tabla gastos).</summary>
public sealed class GastoConfig : IEntityTypeConfiguration<Gasto>
{
    public void Configure(EntityTypeBuilder<Gasto> e)
    {
        e.ToTable("gastos");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.Fecha).HasColumnName("fecha");
        e.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(30).IsRequired();
        e.Property(x => x.Concepto).HasColumnName("concepto").HasMaxLength(200).IsRequired();
        e.Property(x => x.Importe).HasColumnName("importe").HasPrecision(12, 2);
        e.Property(x => x.FormaPagoSat).HasColumnName("forma_pago_sat").HasMaxLength(2).IsRequired();
        e.Property(x => x.Comprobante).HasColumnName("comprobante").HasMaxLength(100);
        e.Property(x => x.CorteCajaId).HasColumnName("corte_caja_id");
        e.Property(x => x.Notas).HasColumnName("notas");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.CreadoUtc).HasColumnName("creado_utc");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.CorteCaja).WithMany().HasForeignKey(x => x.CorteCajaId)
            .OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.SucursalId, x.Fecha }).HasDatabaseName("ix_gastos_sucursal_fecha");
        e.HasIndex(x => x.Categoria).HasDatabaseName("ix_gastos_categoria");
    }
}

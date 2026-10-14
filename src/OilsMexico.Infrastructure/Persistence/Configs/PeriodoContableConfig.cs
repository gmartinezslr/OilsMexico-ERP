using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

/// <summary>
/// Config EF de <see cref="PeriodoContable"/> (snake_case, tabla periodos_contables).
/// Sin FK a sucursales: el índice único (sucursal, año, mes) es la bandera de cierre
/// y también el guard de concurrencia del cierre (el mismo patrón que asientos_contables).
/// </summary>
public sealed class PeriodoContableConfig : IEntityTypeConfiguration<PeriodoContable>
{
    public void Configure(EntityTypeBuilder<PeriodoContable> e)
    {
        e.ToTable("periodos_contables");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.Anio).HasColumnName("anio");
        e.Property(x => x.Mes).HasColumnName("mes");
        e.Property(x => x.UsuarioCierreId).HasColumnName("usuario_cierre_id");
        e.Property(x => x.CerradoUtc).HasColumnName("cerrado_utc");
        e.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(400);
        e.HasIndex(x => new { x.SucursalId, x.Anio, x.Mes }).IsUnique()
            .HasDatabaseName("uq_periodos_contables_sucursal_periodo");
    }
}
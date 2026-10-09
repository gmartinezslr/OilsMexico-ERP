using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CuotaVendedorConfig : IEntityTypeConfiguration<CuotaVendedor>
{
    public void Configure(EntityTypeBuilder<CuotaVendedor> e)
    {
        e.ToTable("cuotas_vendedor");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.VendedorId).HasColumnName("vendedor_id");
        e.Property(x => x.Anio).HasColumnName("anio");
        e.Property(x => x.Mes).HasColumnName("mes");
        e.Property(x => x.MetaDinero).HasColumnName("meta_dinero").HasPrecision(12, 2);
        e.Property(x => x.MetaLitros).HasColumnName("meta_litros").HasPrecision(12, 2);
        // Enums como texto: legibles en SQL y sin romperse al reordenar el enum.
        e.Property(x => x.Operador).HasColumnName("operador").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.AccionIncumplimiento).HasColumnName("accion_incumplimiento").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.MontoPagoMinimo).HasColumnName("monto_pago_minimo").HasPrecision(12, 2).HasDefaultValue(0m);
        e.Property(x => x.Notas).HasColumnName("notas");
        e.Property(x => x.CreadoUtc).HasColumnName("creado_utc");
        e.Property(x => x.ActualizadoUtc).HasColumnName("actualizado_utc");

        // Una sola cuota por vendedor y periodo: sin esto se podrían crear metas duplicadas
        // y el motor tomaría una arbitraria.
        e.HasIndex(x => new { x.VendedorId, x.Anio, x.Mes }).IsUnique()
            .HasDatabaseName("uq_cuotas_vendedor_periodo");
        e.HasIndex(x => new { x.Anio, x.Mes }).HasDatabaseName("ix_cuotas_periodo");
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ComisionHistorialConfig : IEntityTypeConfiguration<ComisionHistorial>
{
    public void Configure(EntityTypeBuilder<ComisionHistorial> e)
    {
        e.ToTable("comisiones_historial");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.VendedorId).HasColumnName("vendedor_id");
        e.Property(x => x.Anio).HasColumnName("anio");
        e.Property(x => x.Mes).HasColumnName("mes");
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);

        e.Property(x => x.DineroReal).HasColumnName("dinero_real").HasPrecision(12, 2);
        e.Property(x => x.LitrosReales).HasColumnName("litros_reales").HasPrecision(12, 2);
        e.Property(x => x.CobradoBruto).HasColumnName("cobrado_bruto").HasPrecision(12, 2);
        e.Property(x => x.FacturasCobradas).HasColumnName("facturas_cobradas");

        e.Property(x => x.MetaDinero).HasColumnName("meta_dinero").HasPrecision(12, 2);
        e.Property(x => x.MetaLitros).HasColumnName("meta_litros").HasPrecision(12, 2);
        e.Property(x => x.Operador).HasColumnName("operador").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.AccionIncumplimiento).HasColumnName("accion_incumplimiento").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.MontoPagoMinimo).HasColumnName("monto_pago_minimo").HasPrecision(12, 2);

        e.Property(x => x.PctDinero).HasColumnName("pct_dinero").HasPrecision(8, 2);
        e.Property(x => x.PctLitros).HasColumnName("pct_litros").HasPrecision(8, 2);
        e.Property(x => x.CumplioMeta).HasColumnName("cumplio_meta");
        e.Property(x => x.DetalleEvaluacion).HasColumnName("detalle_evaluacion").HasMaxLength(300);

        e.Property(x => x.ComisionBase).HasColumnName("comision_base").HasPrecision(12, 2);
        e.Property(x => x.ComisionBono).HasColumnName("comision_bono").HasPrecision(12, 2);
        e.Property(x => x.ComisionFinal).HasColumnName("comision_final").HasPrecision(12, 2);
        e.Property(x => x.AplicoPagoMinimo).HasColumnName("aplico_pago_minimo");
        e.Property(x => x.ProductosSinTabulador).HasColumnName("productos_sin_tabulador");
        e.Property(x => x.DesgloseJson).HasColumnName("desglose_json");

        e.Property(x => x.CalculadoUtc).HasColumnName("calculado_utc");
        e.Property(x => x.CerradoPorUsuarioId).HasColumnName("cerrado_por_usuario_id");
        e.Property(x => x.CerradoUtc).HasColumnName("cerrado_utc");
        e.Property(x => x.PagadoUtc).HasColumnName("pagado_utc");
        e.Property(x => x.Notas).HasColumnName("notas");

        // Un cierre por vendedor y periodo. Recalcular = reabrir el mismo registro (ver
        // ComisionesService.ReabrirPeriodoAsync), nunca crear uno nuevo encima.
        e.HasIndex(x => new { x.VendedorId, x.Anio, x.Mes }).IsUnique()
            .HasDatabaseName("uq_comisiones_historial_periodo");
        e.HasIndex(x => new { x.Anio, x.Mes, x.Estado }).HasDatabaseName("ix_comisiones_periodo");
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

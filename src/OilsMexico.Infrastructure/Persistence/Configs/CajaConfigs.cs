using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CorteCajaConfig : IEntityTypeConfiguration<CorteCaja>
{
    public void Configure(EntityTypeBuilder<CorteCaja> e)
    {
        e.ToTable("cortes_caja");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.UsuarioAperturaId).HasColumnName("usuario_apertura_id");
        e.Property(x => x.UsuarioAperturaNombre).HasColumnName("usuario_apertura_nombre").HasMaxLength(100);
        e.Property(x => x.UsuarioCierreId).HasColumnName("usuario_cierre_id");
        e.Property(x => x.FechaAperturaUtc).HasColumnName("fecha_apertura_utc");
        e.Property(x => x.FechaCierreUtc).HasColumnName("fecha_cierre_utc");
        e.Property(x => x.FondoInicial).HasColumnName("fondo_inicial").HasPrecision(12, 2);
        e.Property(x => x.TotalEfectivoSistema).HasColumnName("total_efectivo_sistema").HasPrecision(12, 2);
        e.Property(x => x.TotalTarjetaSistema).HasColumnName("total_tarjeta_sistema").HasPrecision(12, 2);
        e.Property(x => x.TotalTransferSistema).HasColumnName("total_transfer_sistema").HasPrecision(12, 2);
        e.Property(x => x.TotalOtrosSistema).HasColumnName("total_otros_sistema").HasPrecision(12, 2);
        e.Property(x => x.TotalVentasSistema).HasColumnName("total_ventas_sistema").HasPrecision(12, 2);
        e.Property(x => x.NumVentas).HasColumnName("num_ventas");
        e.Property(x => x.EfectivoContado).HasColumnName("efectivo_contado").HasPrecision(12, 2);
        e.Property(x => x.Diferencia).HasColumnName("diferencia").HasPrecision(12, 2);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20);
        e.Property(x => x.Observaciones).HasColumnName("observaciones");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasIndex(x => new { x.SucursalId, x.Estado }).HasDatabaseName("ix_cortes_sucursal_estado");
    }
}

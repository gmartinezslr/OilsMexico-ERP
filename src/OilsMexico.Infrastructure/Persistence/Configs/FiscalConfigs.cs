using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

/// <summary>Configs fiscal: notas_credito + venta_cobros.</summary>
public sealed class NotaCreditoConfig : IEntityTypeConfiguration<NotaCredito>
{
    public void Configure(EntityTypeBuilder<NotaCredito> e)
    {
        e.ToTable("notas_credito");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.FolioInterno).HasColumnName("folio_interno").HasMaxLength(30).IsRequired();
        e.HasIndex(x => x.FolioInterno).IsUnique();
        e.Property(x => x.FacturaOrigenId).HasColumnName("factura_origen_id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.UuidFacturaOrigen).HasColumnName("uuid_factura_origen");
        e.Property(x => x.UuidSat).HasColumnName("uuid_sat");
        e.HasIndex(x => x.UuidSat).IsUnique();
        e.Property(x => x.FechaEmision).HasColumnName("fecha_emision");
        e.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        e.Property(x => x.Iva).HasColumnName("iva").HasPrecision(12, 2);
        e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
        e.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(20);
        e.Property(x => x.UsoCfdi).HasColumnName("uso_cfdi").HasMaxLength(5);
        e.Property(x => x.TipoRelacion).HasColumnName("tipo_relacion").HasMaxLength(2);
        e.Property(x => x.XmlSellado).HasColumnName("xml_sellado");
        e.Property(x => x.SelloDigital).HasColumnName("sello_digital");
        e.Property(x => x.CadenaOriginal).HasColumnName("cadena_original");
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.MotivoCancelacion).HasColumnName("motivo_cancelacion").HasMaxLength(300);
        e.Property(x => x.RepusoStock).HasColumnName("repuso_stock");
        e.Property(x => x.Observaciones).HasColumnName("observaciones");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.FacturaOrigen).WithMany().HasForeignKey(x => x.FacturaOrigenId);
        e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId);
    }
}

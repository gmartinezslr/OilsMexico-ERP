using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class ComplementoPagoConfig : IEntityTypeConfiguration<ComplementoPago>
{
    public void Configure(EntityTypeBuilder<ComplementoPago> e)
    {
        e.ToTable("complementos_pago");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.FolioInterno).HasColumnName("folio_interno").HasMaxLength(30).IsRequired();
        e.HasIndex(x => x.FolioInterno).IsUnique();
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.FechaEmision).HasColumnName("fecha_emision");
        e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
        e.Property(x => x.UuidSat).HasColumnName("uuid_sat");
        e.HasIndex(x => x.UuidSat).IsUnique();
        e.Property(x => x.XmlSellado).HasColumnName("xml_sellado");
        e.Property(x => x.SelloDigital).HasColumnName("sello_digital");
        e.Property(x => x.CadenaOriginal).HasColumnName("cadena_original");
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20);
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId);
    }
}

public sealed class ComplementoPagoDetalleConfig : IEntityTypeConfiguration<ComplementoPagoDetalle>
{
    public void Configure(EntityTypeBuilder<ComplementoPagoDetalle> e)
    {
        e.ToTable("complemento_pago_detalle");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ComplementoPagoId).HasColumnName("complemento_pago_id");
        e.Property(x => x.FacturaId).HasColumnName("factura_id");
        e.Property(x => x.VentaCobroId).HasColumnName("venta_cobro_id");
        e.Property(x => x.UuidFactura).HasColumnName("uuid_factura");
        e.Property(x => x.FolioFactura).HasColumnName("folio_factura").HasMaxLength(30);
        e.Property(x => x.NumParcialidad).HasColumnName("num_parcialidad");
        e.Property(x => x.ImpSaldoAnt).HasColumnName("imp_saldo_ant").HasPrecision(12, 2);
        e.Property(x => x.ImpPagado).HasColumnName("imp_pagado").HasPrecision(12, 2);
        e.Property(x => x.ImpSaldoInsoluto).HasColumnName("imp_saldo_insoluto").HasPrecision(12, 2);
        e.Property(x => x.Moneda).HasColumnName("moneda").HasMaxLength(3);
        e.HasOne(x => x.ComplementoPago).WithMany(r => r.Documentos)
            .HasForeignKey(x => x.ComplementoPagoId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Factura).WithMany().HasForeignKey(x => x.FacturaId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class NotaCreditoDetalleConfig : IEntityTypeConfiguration<NotaCreditoDetalle>
{
    public void Configure(EntityTypeBuilder<NotaCreditoDetalle> e)
    {
        e.ToTable("nota_credito_detalle");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.NotaCreditoId).HasColumnName("nota_credito_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.UnidadNombre).HasColumnName("unidad_nombre").HasMaxLength(20);
        e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(12, 2);
        e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
        e.Property(x => x.Importe).HasColumnName("importe").HasPrecision(12, 2);
        e.Property(x => x.LoteId).HasColumnName("lote_id");
        e.HasOne(x => x.NotaCredito).WithMany(n => n.Detalles)
            .HasForeignKey(x => x.NotaCreditoId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
    }
}

public sealed class VentaCobroConfig : IEntityTypeConfiguration<VentaCobro>
{
    public void Configure(EntityTypeBuilder<VentaCobro> e)
    {
        e.ToTable("venta_cobros");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.FacturaId).HasColumnName("factura_id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.Monto).HasColumnName("monto").HasPrecision(12, 2);
        e.Property(x => x.FormaPagoSat).HasColumnName("forma_pago_sat").HasMaxLength(2);
        e.Property(x => x.FechaPagoUtc).HasColumnName("fecha_pago_utc");
        e.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(100);
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.ComplementoPagoId).HasColumnName("complemento_pago_id");
        e.Property(x => x.CreadoUtc).HasColumnName("creado_utc");
        e.HasOne(x => x.Factura).WithMany().HasForeignKey(x => x.FacturaId);
        e.HasOne(x => x.ComplementoPago).WithMany().HasForeignKey(x => x.ComplementoPagoId);
        e.HasIndex(x => x.FacturaId).HasDatabaseName("ix_venta_cobros_factura");
    }
}

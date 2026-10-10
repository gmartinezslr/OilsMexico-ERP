using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CompraConfig : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> e)
    {
        e.ToTable("compras");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.FolioInterno).HasColumnName("folio_interno").HasMaxLength(30).IsRequired();
        e.Property(x => x.ProveedorId).HasColumnName("proveedor_id");
        e.Property(x => x.FolioProveedor).HasColumnName("folio_proveedor").HasMaxLength(50);
        e.Property(x => x.FechaEmision).HasColumnName("fecha_emision");
        e.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        e.Property(x => x.Iva).HasColumnName("iva").HasPrecision(12, 2);
        e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.EstadoPago).HasColumnName("estado_pago").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.MontoPagado).HasColumnName("monto_pagado").HasPrecision(12, 2);
        e.Property(x => x.Notas).HasColumnName("notas");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId);
    }
}

public sealed class CompraDetalleConfig : IEntityTypeConfiguration<CompraDetalle>
{
    public void Configure(EntityTypeBuilder<CompraDetalle> e)
    {
        e.ToTable("compra_detalle");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CompraId).HasColumnName("compra_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.UnidadMedidaId).HasColumnName("unidad_medida_id");
        e.Property(x => x.UnidadNombre).HasColumnName("unidad_nombre").HasMaxLength(20);
        e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(12, 4);
        e.Property(x => x.FactorConversion).HasColumnName("factor_conversion").HasPrecision(10, 4);
        e.Property(x => x.CantidadRecibida).HasColumnName("cantidad_recibida").HasPrecision(12, 4);
        e.Property(x => x.LitrosRecibidos).HasColumnName("litros_recibidos").HasPrecision(12, 2);
        e.Property(x => x.CostoUnitario).HasColumnName("costo_unitario").HasPrecision(12, 2);
        e.Property(x => x.NumeroLote).HasColumnName("numero_lote").HasMaxLength(50);
        e.Property(x => x.FechaCaducidad).HasColumnName("fecha_caducidad");
        e.HasOne(x => x.Compra).WithMany(c => c.Detalles)
            .HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
    }
}

public sealed class CompraPagoConfig : IEntityTypeConfiguration<CompraPago>
{
    public void Configure(EntityTypeBuilder<CompraPago> e)
    {
        e.ToTable("compra_pagos");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CompraId).HasColumnName("compra_id");
        e.Property(x => x.Monto).HasColumnName("monto").HasPrecision(12, 2);
        e.Property(x => x.FormaPago).HasColumnName("forma_pago").HasMaxLength(2);
        e.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(100);
        e.Property(x => x.FechaUtc).HasColumnName("fecha_utc");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.HasOne(x => x.Compra).WithMany().HasForeignKey(x => x.CompraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

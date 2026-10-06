using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class ProductoConfig : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> e)
    {
        e.ToTable("productos");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        e.HasIndex(x => x.Sku).IsUnique();
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        e.Property(x => x.Marca).HasColumnName("marca").HasMaxLength(50).IsRequired();
        e.Property(x => x.Viscosidad).HasColumnName("viscosidad").HasMaxLength(20).IsRequired();
        e.Property(x => x.TipoBase).HasColumnName("tipo_base").HasMaxLength(30).IsRequired();
        e.Property(x => x.DescripcionTecnica).HasColumnName("descripcion_tecnica");
        e.Property(x => x.PrecioVenta).HasColumnName("precio_venta").HasPrecision(12, 2);
        e.Property(x => x.PrecioMayoreo).HasColumnName("precio_mayoreo").HasPrecision(12, 2);
        e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
    }
}

public sealed class UnidadMedidaConfig : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> e)
    {
        e.ToTable("unidades_medida");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.UnidadNombre).HasColumnName("unidad_nombre").HasMaxLength(20).IsRequired();
        e.Property(x => x.FactorConversion).HasColumnName("factor_conversion").HasPrecision(10, 4);
        e.Property(x => x.CodigoBarra).HasColumnName("codigo_barra").HasMaxLength(50);
        e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
        e.HasOne(x => x.Producto).WithMany(p => p.Unidades)
            .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SucursalConfig : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> e)
    {
        e.ToTable("sucursales");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        e.Property(x => x.CodigoSucursal).HasColumnName("codigo_sucursal").HasMaxLength(10).IsRequired();
        e.HasIndex(x => x.CodigoSucursal).IsUnique();
        e.Property(x => x.Direccion).HasColumnName("direccion").IsRequired();
        e.Property(x => x.RfcEmisor).HasColumnName("rfc_emisor").HasMaxLength(13).IsRequired();
        e.Property(x => x.Activa).HasColumnName("activa").HasDefaultValue(true);

        // Datos fiscales del emisor CFDI 4.0 + dirección desglosada + contacto.
        // Columnas anulables: SchemaPatch las agrega a BDs existentes sin DEFAULT.
        e.Property(x => x.RazonSocial).HasColumnName("razon_social").HasMaxLength(150);
        e.Property(x => x.RegimenFiscal).HasColumnName("regimen_fiscal").HasMaxLength(10);
        e.Property(x => x.CodigoPostal).HasColumnName("codigo_postal").HasMaxLength(10);
        e.Property(x => x.Calle).HasColumnName("calle").HasMaxLength(150);
        e.Property(x => x.NumeroExterior).HasColumnName("numero_exterior").HasMaxLength(20);
        e.Property(x => x.NumeroInterior).HasColumnName("numero_interior").HasMaxLength(20);
        e.Property(x => x.Colonia).HasColumnName("colonia").HasMaxLength(200);
        e.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(100);
        e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(150);
        e.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(60);
        e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(20);
        e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
        e.HasIndex(x => x.CodigoPostal);
    }
}

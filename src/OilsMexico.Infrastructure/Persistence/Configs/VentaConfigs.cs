using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class FacturaConfig : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> e)
    {
        e.ToTable("facturas");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.FolioInterno).HasColumnName("folio_interno").HasMaxLength(20).IsRequired();
        e.Property(x => x.UuidSat).HasColumnName("uuid_sat");
        e.HasIndex(x => x.UuidSat).IsUnique();
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.FechaEmision).HasColumnName("fecha_emision");
        // Atribución de la venta (crédito de comisión). Nullable sólo por el histórico sin vendedor conocido.
        e.Property(x => x.VendedorId).HasColumnName("vendedor_id");
        e.HasIndex(x => x.VendedorId).HasDatabaseName("ix_facturas_vendedor");
        e.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        e.Property(x => x.Iva).HasColumnName("iva").HasPrecision(12, 2);
        e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
        e.Property(x => x.XmlSellado).HasColumnName("xml_sellado");
        e.Property(x => x.SelloDigital).HasColumnName("sello_digital");
        e.Property(x => x.CadenaOriginal).HasColumnName("cadena_original");
        e.Property(x => x.MetodoPagoSat).HasColumnName("metodo_pago_sat").HasMaxLength(3);
        e.Property(x => x.FormaPagoSat).HasColumnName("forma_pago_sat").HasMaxLength(2);
        e.Property(x => x.UsoCfdi).HasColumnName("uso_cfdi").HasMaxLength(5);
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.MotivoCancelacion).HasColumnName("motivo_cancelacion").HasMaxLength(300);
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
        e.HasOne(x => x.Cliente).WithMany(c => c.Facturas).HasForeignKey(x => x.ClienteId);
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FacturaDetalleConfig : IEntityTypeConfiguration<FacturaDetalle>
{
    public void Configure(EntityTypeBuilder<FacturaDetalle> e)
    {
        e.ToTable("factura_detalle");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.FacturaId).HasColumnName("factura_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.UnidadMedidaId).HasColumnName("unidad_medida_id");
        e.Property(x => x.UnidadNombre).HasColumnName("unidad_nombre").HasMaxLength(20);
        e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(12, 2);
        e.Property(x => x.LitrosDescontados).HasColumnName("litros_descontados").HasPrecision(12, 2);
        e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
        e.Property(x => x.Importe).HasColumnName("importe").HasPrecision(12, 2);
        e.Property(x => x.LoteId).HasColumnName("lote_id");
        e.HasOne(x => x.Factura).WithMany(f => f.Detalles)
            .HasForeignKey(x => x.FacturaId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
    }
}

public sealed class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> e)
    {
        e.ToTable("clientes");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        e.Property(x => x.Rfc).HasColumnName("rfc").HasMaxLength(13).HasDefaultValue("XAXX010101000");
        e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(20);
        e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
        e.Property(x => x.Direccion).HasColumnName("direccion");
        e.Property(x => x.TipoPrecio).HasColumnName("tipo_precio").HasMaxLength(10).HasDefaultValue("menudeo");
        e.Property(x => x.RegimenFiscal).HasColumnName("regimen_fiscal").HasMaxLength(3);
        e.Property(x => x.CodigoPostal).HasColumnName("codigo_postal").HasMaxLength(10);
        e.HasIndex(x => x.CodigoPostal).HasDatabaseName("ix_clientes_cp");
        e.Property(x => x.Calle).HasColumnName("calle").HasMaxLength(150);
        e.Property(x => x.NumeroExterior).HasColumnName("numero_exterior").HasMaxLength(20);
        e.Property(x => x.NumeroInterior).HasColumnName("numero_interior").HasMaxLength(20);
        e.Property(x => x.Colonia).HasColumnName("colonia").HasMaxLength(200);
        e.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(100);
        e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(150);
        e.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(60).HasDefaultValue("México");
        e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        // Dueño comercial actual del cliente (mutable). NULL = sin dueño (p.ej. Público en general).
        e.Property(x => x.VendedorId).HasColumnName("vendedor_id");
        e.HasIndex(x => x.VendedorId).HasDatabaseName("ix_clientes_vendedor");
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> e)
    {
        e.ToTable("usuarios");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        e.Property(x => x.Correo).HasColumnName("correo").HasMaxLength(200);
        e.Property(x => x.PinHash).HasColumnName("pin_hash").HasMaxLength(64);
        e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(128).IsRequired();
        e.Property(x => x.Rol).HasColumnName("rol").HasMaxLength(20);
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        e.Property(x => x.IntentosFallidos).HasColumnName("intentos_fallidos").HasDefaultValue(0);
        e.Property(x => x.BloqueosTemporales).HasColumnName("bloqueos_temporales").HasDefaultValue(0);
        e.Property(x => x.BloqueadoHastaUtc).HasColumnName("bloqueado_hasta_utc");
        e.Property(x => x.BloqueadoDefinitivamente).HasColumnName("bloqueado_definitivamente").HasDefaultValue(false);
        e.Property(x => x.SesionToken).HasColumnName("sesion_token").HasMaxLength(64);
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class ContactoTerceroConfig : IEntityTypeConfiguration<ContactoTercero>
{
    public void Configure(EntityTypeBuilder<ContactoTercero> e)
    {
        e.ToTable("terceros_contactos", t => t.HasCheckConstraint(
            "ck_terceros_contactos_tercero",
            "(cliente_id IS NOT NULL AND proveedor_id IS NULL) OR (cliente_id IS NULL AND proveedor_id IS NOT NULL)"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.ProveedorId).HasColumnName("proveedor_id");
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        e.Property(x => x.Puesto).HasColumnName("puesto").HasMaxLength(120);
        e.Property(x => x.Area).HasColumnName("area").HasMaxLength(120);
        e.Property(x => x.EsPrincipal).HasColumnName("es_principal");
        e.Property(x => x.EsRespaldo).HasColumnName("es_respaldo");
        e.Property(x => x.PuedeRecibirPedidos).HasColumnName("puede_recibir_pedidos");
        e.Property(x => x.PuedeRecibirNotificaciones).HasColumnName("puede_recibir_notificaciones");
        e.Property(x => x.AlcanceAutorizacion).HasColumnName("alcance_autorizacion").HasMaxLength(500);
        e.Property(x => x.Horario).HasColumnName("horario").HasMaxLength(200);
        e.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(500);
        e.HasOne(x => x.Cliente).WithMany(x => x.Contactos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Proveedor).WithMany(x => x.Contactos).HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.ClienteId, x.EsPrincipal }).IsUnique()
            .HasFilter("es_principal = TRUE AND cliente_id IS NOT NULL").HasDatabaseName("uq_contactos_cliente_principal");
        e.HasIndex(x => x.ProveedorId).IsUnique().HasFilter("es_principal = TRUE AND proveedor_id IS NOT NULL").HasDatabaseName("uq_contactos_proveedor_principal");
        e.HasIndex(x => x.ClienteId).IsUnique().HasFilter("es_respaldo = TRUE AND cliente_id IS NOT NULL").HasDatabaseName("uq_contactos_cliente_respaldo");
    }
}

public sealed class CorreoTerceroConfig : IEntityTypeConfiguration<CorreoTercero>
{
    public void Configure(EntityTypeBuilder<CorreoTercero> e)
    {
        e.ToTable("terceros_correos", t => t.HasCheckConstraint(
            "ck_terceros_correos_tercero",
            "(cliente_id IS NOT NULL AND proveedor_id IS NULL) OR (cliente_id IS NULL AND proveedor_id IS NOT NULL)"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.ProveedorId).HasColumnName("proveedor_id");
        e.Property(x => x.ContactoTerceroId).HasColumnName("contacto_tercero_id");
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
        e.Property(x => x.Direccion).HasColumnName("direccion").HasMaxLength(254).IsRequired();
        e.Property(x => x.EsPrincipal).HasColumnName("es_principal");
        e.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(500);
        e.HasOne(x => x.Cliente).WithMany(x => x.Correos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Proveedor).WithMany(x => x.Correos).HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Contacto).WithMany().HasForeignKey(x => x.ContactoTerceroId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.ClienteId).IsUnique().HasFilter("es_principal = TRUE AND cliente_id IS NOT NULL").HasDatabaseName("uq_correos_cliente_principal");
        e.HasIndex(x => x.ProveedorId).IsUnique().HasFilter("es_principal = TRUE AND proveedor_id IS NOT NULL").HasDatabaseName("uq_correos_proveedor_principal");
    }
}

public sealed class TelefonoTerceroConfig : IEntityTypeConfiguration<TelefonoTercero>
{
    public void Configure(EntityTypeBuilder<TelefonoTercero> e)
    {
        e.ToTable("terceros_telefonos", t => t.HasCheckConstraint(
            "ck_terceros_telefonos_tercero",
            "(cliente_id IS NOT NULL AND proveedor_id IS NULL) OR (cliente_id IS NULL AND proveedor_id IS NOT NULL)"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.ProveedorId).HasColumnName("proveedor_id");
        e.Property(x => x.ContactoTerceroId).HasColumnName("contacto_tercero_id");
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
        e.Property(x => x.Numero).HasColumnName("numero").HasMaxLength(40).IsRequired();
        e.Property(x => x.TieneWhatsApp).HasColumnName("tiene_whatsapp");
        e.Property(x => x.EsPrincipal).HasColumnName("es_principal");
        e.Property(x => x.Horario).HasColumnName("horario").HasMaxLength(200);
        e.Property(x => x.Notas).HasColumnName("notas").HasMaxLength(500);
        e.HasOne(x => x.Cliente).WithMany(x => x.Telefonos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Proveedor).WithMany(x => x.Telefonos).HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Contacto).WithMany().HasForeignKey(x => x.ContactoTerceroId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.ClienteId).IsUnique().HasFilter("es_principal = TRUE AND cliente_id IS NOT NULL").HasDatabaseName("uq_telefonos_cliente_principal");
        e.HasIndex(x => x.ProveedorId).IsUnique().HasFilter("es_principal = TRUE AND proveedor_id IS NOT NULL").HasDatabaseName("uq_telefonos_proveedor_principal");
    }
}

public sealed class DireccionTerceroConfig : IEntityTypeConfiguration<DireccionTercero>
{
    public void Configure(EntityTypeBuilder<DireccionTercero> e)
    {
        e.ToTable("terceros_direcciones", t => t.HasCheckConstraint(
            "ck_terceros_direcciones_tercero",
            "(cliente_id IS NOT NULL AND proveedor_id IS NULL) OR (cliente_id IS NULL AND proveedor_id IS NOT NULL)"));
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.ProveedorId).HasColumnName("proveedor_id");
        e.Property(x => x.ContactoRecibeId).HasColumnName("contacto_recibe_id");
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
        e.Property(x => x.Calle).HasColumnName("calle").HasMaxLength(150);
        e.Property(x => x.NumeroExterior).HasColumnName("numero_exterior").HasMaxLength(20);
        e.Property(x => x.NumeroInterior).HasColumnName("numero_interior").HasMaxLength(20);
        e.Property(x => x.Colonia).HasColumnName("colonia").HasMaxLength(200);
        e.Property(x => x.CodigoPostal).HasColumnName("codigo_postal").HasMaxLength(10);
        e.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(100);
        e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(150);
        e.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(60).HasDefaultValue("México");
        e.Property(x => x.EsPredeterminada).HasColumnName("es_predeterminada");
        e.Property(x => x.Horario).HasColumnName("horario").HasMaxLength(200);
        e.Property(x => x.Instrucciones).HasColumnName("instrucciones").HasMaxLength(500);
        e.HasOne(x => x.Cliente).WithMany(x => x.Direcciones).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Proveedor).WithMany(x => x.Direcciones).HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.ContactoRecibe).WithMany().HasForeignKey(x => x.ContactoRecibeId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.ClienteId, x.Tipo }).IsUnique()
            .HasFilter("es_predeterminada = TRUE AND cliente_id IS NOT NULL").HasDatabaseName("uq_direcciones_cliente_predeterminada_tipo");
        e.HasIndex(x => new { x.ProveedorId, x.Tipo }).IsUnique()
            .HasFilter("es_predeterminada = TRUE AND proveedor_id IS NOT NULL").HasDatabaseName("uq_direcciones_proveedor_predeterminada_tipo");
    }
}

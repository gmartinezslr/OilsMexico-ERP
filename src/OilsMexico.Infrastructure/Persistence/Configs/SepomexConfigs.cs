using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

/// <summary>
/// Tabla: codigos_postales. Equivalente PostgreSQL del DDL SQL Server
/// [dbo].[CodigosPostales] de la imagen (mismas 15 columnas).
/// </summary>
public sealed class CodigoPostalConfig : IEntityTypeConfiguration<CodigoPostal>
{
    public void Configure(EntityTypeBuilder<CodigoPostal> e)
    {
        e.ToTable("codigos_postales");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Codigo).HasColumnName("codigo_postal").HasMaxLength(10).IsRequired();
        e.HasIndex(x => x.Codigo).HasDatabaseName("ix_codigos_postales_cp");
        e.Property(x => x.Asentamiento).HasColumnName("asentamiento").HasMaxLength(200).IsRequired();
        e.HasIndex(x => x.Asentamiento).HasDatabaseName("ix_codigos_postales_asentamiento");
        e.Property(x => x.TipoAsentamiento).HasColumnName("tipo_asentamiento").HasMaxLength(60).IsRequired();
        e.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150).IsRequired();
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Estado).HasDatabaseName("ix_codigos_postales_estado");
        e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(150);
        e.Property(x => x.Dcp).HasColumnName("dcp").HasMaxLength(150);
        e.Property(x => x.EstadoId).HasColumnName("estado_id").HasMaxLength(10);
        e.Property(x => x.Oficina).HasColumnName("oficina").HasMaxLength(10);
        e.Property(x => x.Ccp).HasColumnName("ccp").HasMaxLength(10);
        e.Property(x => x.TipoAsentamientoId).HasColumnName("tipo_asentamiento_id").HasMaxLength(10);
        e.Property(x => x.MunicipioId).HasColumnName("municipio_id").HasMaxLength(10);
        e.Property(x => x.AsentamientoId).HasColumnName("asentamiento_id").HasMaxLength(10);
        e.Property(x => x.Zona).HasColumnName("zona").HasMaxLength(20);
        e.Property(x => x.CiudadId).HasColumnName("ciudad_id").HasMaxLength(10);
        // Un CP tiene N asentamientos: el par (codigo, asentamiento_id) es único.
        e.HasIndex(x => new { x.Codigo, x.AsentamientoId })
            .IsUnique().HasDatabaseName("uq_codigos_postales_cp_asentamiento");
    }
}

public sealed class ProveedorConfig : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> e)
    {
        e.ToTable("proveedores");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(150).IsRequired();
        e.Property(x => x.Rfc).HasColumnName("rfc").HasMaxLength(13).HasDefaultValue("XAXX010101000");
        e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(20);
        e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
        e.Property(x => x.Calle).HasColumnName("calle").HasMaxLength(150);
        e.Property(x => x.NumeroExterior).HasColumnName("numero_exterior").HasMaxLength(20);
        e.Property(x => x.NumeroInterior).HasColumnName("numero_interior").HasMaxLength(20);
        e.Property(x => x.Colonia).HasColumnName("colonia").HasMaxLength(200);
        e.Property(x => x.CodigoPostal).HasColumnName("codigo_postal").HasMaxLength(10);
        e.HasIndex(x => x.CodigoPostal).HasDatabaseName("ix_proveedores_cp");
        e.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(100);
        e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(150);
        e.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(60).HasDefaultValue("México");
        e.Property(x => x.Direccion).HasColumnName("direccion");
        e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
        // Situación fiscal declarada ante el SAT (valor por defecto: Actual = 1).
        e.Property(x => x.SituacionFiscal).HasColumnName("situacion_fiscal")
            .HasDefaultValue(OilsMexico.Domain.Enums.SituacionFiscal.Actual);
    }
}

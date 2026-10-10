using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

/// <summary>Mapeo de auditoría e historial de contraseñas a las tablas snake_case creadas por SchemaPatch.</summary>
public sealed class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.ToTable("audit_logs");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.UsuarioNombre).HasColumnName("usuario_nombre").HasMaxLength(150);
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(60).IsRequired();
        e.Property(x => x.Detalle).HasColumnName("detalle");
        e.Property(x => x.Anterior).HasColumnName("anterior");
        e.Property(x => x.Nuevo).HasColumnName("nuevo");
        e.Property(x => x.FechaUtc).HasColumnName("fecha_utc");
        e.HasIndex(x => x.UsuarioId).HasDatabaseName("ix_audit_logs_usuario");
        e.HasIndex(x => x.Tipo).HasDatabaseName("ix_audit_logs_tipo");
        e.HasIndex(x => x.FechaUtc).HasDatabaseName("ix_audit_logs_fecha");
        e.HasOne(x => x.Usuario).WithMany(x => x.AuditLogs).HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PasswordHistoryConfig : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> e)
    {
        e.ToTable("password_histories");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(128).IsRequired();
        e.Property(x => x.FechaCambioUtc).HasColumnName("fecha_cambio_utc");
        e.HasIndex(x => x.UsuarioId).HasDatabaseName("ix_password_histories_usuario");
        e.HasOne(x => x.Usuario).WithMany(x => x.PasswordHistories).HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RolPermisoConfig : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> e)
    {
        e.HasKey(rp => new { rp.RolId, rp.PermisoId });
        e.HasOne(rp => rp.Rol).WithMany(r => r.RolPermisos).HasForeignKey(rp => rp.RolId);
        e.HasOne(rp => rp.Permiso).WithMany(p => p.RolPermisos).HasForeignKey(rp => rp.PermisoId);
    }
}
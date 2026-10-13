using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class CuentaBancariaConfig : IEntityTypeConfiguration<CuentaBancaria>
{
    public void Configure(EntityTypeBuilder<CuentaBancaria> e)
    {
        e.ToTable("cuentas_bancarias");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.Banco).HasColumnName("banco").HasMaxLength(60).IsRequired();
        e.Property(x => x.Alias).HasColumnName("alias").HasMaxLength(80).IsRequired();
        e.Property(x => x.NumeroCuenta).HasColumnName("numero_cuenta").HasMaxLength(30);
        e.Property(x => x.Clabe).HasColumnName("clabe").HasMaxLength(18);
        e.Property(x => x.Moneda).HasColumnName("moneda").HasMaxLength(3);
        e.Property(x => x.SaldoInicial).HasColumnName("saldo_inicial").HasPrecision(12, 2);
        e.Property(x => x.Activa).HasColumnName("activa");
        e.HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId);
    }
}

public sealed class MovimientoBancarioConfig : IEntityTypeConfiguration<MovimientoBancario>
{
    public void Configure(EntityTypeBuilder<MovimientoBancario> e)
    {
        e.ToTable("movimientos_bancarios");
        e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.CuentaBancariaId).HasColumnName("cuenta_bancaria_id");
        e.Property(x => x.Fecha).HasColumnName("fecha");
        e.Property(x => x.Concepto).HasColumnName("concepto").HasMaxLength(200).IsRequired();
        e.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(100);
        e.Property(x => x.Naturaleza).HasColumnName("naturaleza").HasConversion<string>().HasMaxLength(10);
        e.Property(x => x.Monto).HasColumnName("monto").HasPrecision(12, 2);
        e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(15);
        e.Property(x => x.VentaCobroId).HasColumnName("venta_cobro_id");
        e.Property(x => x.CompraPagoId).HasColumnName("compra_pago_id");
        e.Property(x => x.ConciliadoUtc).HasColumnName("conciliado_utc");
        e.Property(x => x.ConciliadoPorUsuarioId).HasColumnName("conciliado_por_usuario_id");
        e.Property(x => x.Notas).HasColumnName("notas");
        e.Property(x => x.CreadoUtc).HasColumnName("creado_utc");
        e.HasOne(x => x.CuentaBancaria).WithMany(c => c.Movimientos)
            .HasForeignKey(x => x.CuentaBancariaId).OnDelete(DeleteBehavior.Cascade);
        // Vínculos opcionales: al conciliar se fija uno de los dos (Abono→cobro, Cargo→pago).
        // Restrict para no arrastrar el movimiento bancario si se borra el pago/cobro.
        e.HasOne(x => x.VentaCobro).WithMany().HasForeignKey(x => x.VentaCobroId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.CompraPago).WithMany().HasForeignKey(x => x.CompraPagoId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.CuentaBancariaId).HasDatabaseName("ix_mov_bancarios_cuenta");
        e.HasIndex(x => new { x.CuentaBancariaId, x.Estado }).HasDatabaseName("ix_mov_bancarios_cuenta_estado");
    }
}

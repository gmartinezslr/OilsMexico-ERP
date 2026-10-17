using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence.Configs;

public sealed class ListaPrecioConfig : IEntityTypeConfiguration<ListaPrecio>
{
    public void Configure(EntityTypeBuilder<ListaPrecio> e)
    {
        e.ToTable("listas_precio");
        e.HasKey(x => x.Id);
        e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
        e.Property(x => x.TipoCliente).HasColumnName("tipo_cliente").HasMaxLength(30).IsRequired();
        e.Property(x => x.Activa).HasColumnName("activa").HasDefaultValue(true);
        e.HasMany(x => x.Precios).WithOne(x => x.ListaPrecio).HasForeignKey(x => x.ListaPrecioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PrecioVolumenConfig : IEntityTypeConfiguration<PrecioVolumen>
{
    public void Configure(EntityTypeBuilder<PrecioVolumen> e)
    {
        e.ToTable("precios_volumen");
        e.HasKey(x => x.Id);
        e.Property(x => x.ListaPrecioId).HasColumnName("lista_precio_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.CantidadMinima).HasColumnName("cantidad_minima").HasPrecision(12, 4);
        e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
        e.HasIndex(x => new { x.ListaPrecioId, x.ProductoId, x.CantidadMinima }).IsUnique();
        e.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CompatibilidadVehiculoConfig : IEntityTypeConfiguration<CompatibilidadVehiculo>
{
    public void Configure(EntityTypeBuilder<CompatibilidadVehiculo> e)
    {
        e.ToTable("compatibilidades_vehiculo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Marca).HasColumnName("marca").HasMaxLength(80).IsRequired();
        e.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(100).IsRequired();
        e.Property(x => x.AnioDesde).HasColumnName("anio_desde");
        e.Property(x => x.AnioHasta).HasColumnName("anio_hasta");
        e.Property(x => x.Motor).HasColumnName("motor").HasMaxLength(100).IsRequired();
        e.Property(x => x.Viscosidad).HasColumnName("viscosidad").HasMaxLength(30).IsRequired();
        e.Property(x => x.Norma).HasColumnName("norma").HasMaxLength(100);
        e.Property(x => x.CapacidadLitros).HasColumnName("capacidad_litros").HasPrecision(10, 2);
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.Marca, x.Modelo, x.AnioDesde, x.AnioHasta });
    }
}

public sealed class EvaluacionProveedorConfig : IEntityTypeConfiguration<EvaluacionProveedor>
{
    public void Configure(EntityTypeBuilder<EvaluacionProveedor> e)
    {
        e.ToTable("evaluaciones_proveedor");
        e.HasKey(x => x.Id);
        e.Property(x => x.CompraId).HasColumnName("compra_id");
        e.Property(x => x.ProveedorId).HasColumnName("proveedor_id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.Cumplimiento).HasColumnName("cumplimiento");
        e.Property(x => x.Puntualidad).HasColumnName("puntualidad");
        e.Property(x => x.Comentarios).HasColumnName("comentarios").HasMaxLength(1000);
        e.Property(x => x.FechaUtc).HasColumnName("fecha_utc");
        e.HasIndex(x => x.CompraId).IsUnique();
        e.HasOne<Compra>().WithMany().HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Proveedor>().WithMany().HasForeignKey(x => x.ProveedorId);
    }
}

public sealed class EntregaRepartoConfig : IEntityTypeConfiguration<EntregaReparto>
{
    public void Configure(EntityTypeBuilder<EntregaReparto> e)
    {
        e.ToTable("entregas_reparto");
        e.HasKey(x => x.Id);
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.OrdenVentaId).HasColumnName("orden_venta_id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.Ruta).HasColumnName("ruta").HasMaxLength(150).IsRequired();
        e.Property(x => x.Chofer).HasColumnName("chofer").HasMaxLength(150).IsRequired();
        e.Property(x => x.Vehiculo).HasColumnName("vehiculo").HasMaxLength(150).IsRequired();
        e.Property(x => x.Placas).HasColumnName("placas").HasMaxLength(20);
        e.Property(x => x.Estado).HasColumnName("estado").HasMaxLength(20).IsRequired();
        e.Property(x => x.FechaProgramada).HasColumnName("fecha_programada");
        e.Property(x => x.FechaLiquidacion).HasColumnName("fecha_liquidacion");
        e.HasOne<OrdenVenta>().WithMany().HasForeignKey(x => x.OrdenVentaId);
        e.HasMany(x => x.Detalles).WithOne(x => x.Entrega).HasForeignKey(x => x.EntregaRepartoId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.CartaPorte).WithOne().HasForeignKey<BorradorCartaPorte>(x => x.EntregaRepartoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EntregaRepartoDetalleConfig : IEntityTypeConfiguration<EntregaRepartoDetalle>
{
    public void Configure(EntityTypeBuilder<EntregaRepartoDetalle> e)
    {
        e.ToTable("entregas_reparto_detalle");
        e.HasKey(x => x.Id);
        e.Property(x => x.EntregaRepartoId).HasColumnName("entrega_reparto_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.Producto).HasColumnName("producto").HasMaxLength(150).IsRequired();
        e.Property(x => x.CantidadProgramada).HasColumnName("cantidad_programada").HasPrecision(12, 4);
        e.Property(x => x.CantidadEntregada).HasColumnName("cantidad_entregada").HasPrecision(12, 4);
        e.Property(x => x.CantidadDevuelta).HasColumnName("cantidad_devuelta").HasPrecision(12, 4);
    }
}

public sealed class ControlTamborConfig : IEntityTypeConfiguration<ControlTambor>
{
    public void Configure(EntityTypeBuilder<ControlTambor> e)
    {
        e.ToTable("control_tambores");
        e.HasKey(x => x.Id);
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.ProductoId).HasColumnName("producto_id");
        e.Property(x => x.LitrosPorTambor).HasColumnName("litros_por_tambor").HasPrecision(10, 2);
        e.Property(x => x.TamboresCerrados).HasColumnName("tambores_cerrados");
        e.Property(x => x.LitrosAbiertos).HasColumnName("litros_abiertos").HasPrecision(12, 2);
        e.Property(x => x.ActualizadoUtc).HasColumnName("actualizado_utc");
        e.HasIndex(x => new { x.SucursalId, x.ProductoId }).IsUnique();
        e.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId);
    }
}

public sealed class MovimientoEnvaseRetornableConfig : IEntityTypeConfiguration<MovimientoEnvaseRetornable>
{
    public void Configure(EntityTypeBuilder<MovimientoEnvaseRetornable> e)
    {
        e.ToTable("movimientos_envase_retornable");
        e.HasKey(x => x.Id);
        e.Property(x => x.SucursalId).HasColumnName("sucursal_id");
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.Envase).HasColumnName("envase").HasMaxLength(80).IsRequired();
        e.Property(x => x.Cantidad).HasColumnName("cantidad");
        e.Property(x => x.DepositoUnitario).HasColumnName("deposito_unitario").HasPrecision(12, 2);
        e.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
        e.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(150);
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.FechaUtc).HasColumnName("fecha_utc");
        e.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId);
    }
}

public sealed class BorradorCartaPorteConfig : IEntityTypeConfiguration<BorradorCartaPorte>
{
    public void Configure(EntityTypeBuilder<BorradorCartaPorte> e)
    {
        e.ToTable("borradores_carta_porte");
        e.HasKey(x => x.Id);
        e.Property(x => x.EntregaRepartoId).HasColumnName("entrega_reparto_id");
        e.Property(x => x.RfcOperador).HasColumnName("rfc_operador").HasMaxLength(13).IsRequired();
        e.Property(x => x.PermisoSct).HasColumnName("permiso_sct").HasMaxLength(30).IsRequired();
        e.Property(x => x.NumeroPermisoSct).HasColumnName("numero_permiso_sct").HasMaxLength(50).IsRequired();
        e.Property(x => x.ConfiguracionVehicular).HasColumnName("configuracion_vehicular").HasMaxLength(10).IsRequired();
        e.Property(x => x.Placas).HasColumnName("placas").HasMaxLength(20).IsRequired();
        e.Property(x => x.PesoBrutoKg).HasColumnName("peso_bruto_kg").HasPrecision(12, 2);
        e.Property(x => x.CodigoPostalOrigen).HasColumnName("codigo_postal_origen").HasMaxLength(10).IsRequired();
        e.Property(x => x.CodigoPostalDestino).HasColumnName("codigo_postal_destino").HasMaxLength(10).IsRequired();
        e.Property(x => x.XmlBorrador).HasColumnName("xml_borrador");
        e.Property(x => x.ActualizadoUtc).HasColumnName("actualizado_utc");
        e.HasIndex(x => x.EntregaRepartoId).IsUnique();
    }
}

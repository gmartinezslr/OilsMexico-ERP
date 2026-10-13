using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche idempotente del pipeline comercial (cotizaciones y órdenes de venta).
/// El proyecto no usa migraciones EF en caliente (arranca con <c>EnsureCreated</c> + <c>SchemaPatch</c>),
/// y <c>EnsureCreated</c> NO altera una BD ya existente: por eso estas 4 tablas se crean aquí con
/// IF NOT EXISTS para que una BD de producción previa a este módulo las tenga sin romper el arranque.
/// Nombres de tabla/columna/tipos/FK/índices replican EXACTOS los de la migración
/// 20261012235338_CotizacionesOrdenesVenta (verificado contra las *Config EF).
/// Se invoca desde SchemaPatch.AplicarAsync.
/// </summary>
public static class CotizacionesPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cotizaciones (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL REFERENCES sucursales(id) ON DELETE CASCADE,
                cliente_id integer NOT NULL REFERENCES clientes(id) ON DELETE CASCADE,
                vendedor_id integer REFERENCES usuarios(id) ON DELETE RESTRICT,
                fecha_emision timestamptz NOT NULL DEFAULT now(),
                fecha_vencimiento timestamptz,
                folio_interno varchar(40),
                notas text,
                estado varchar(20) NOT NULL DEFAULT 'Borrador',
                usuario_id integer NOT NULL DEFAULT 0,
                subtotal numeric(12,2) NOT NULL DEFAULT 0,
                iva numeric(12,2) NOT NULL DEFAULT 0,
                total numeric(12,2) NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_cotizaciones_cliente_id ON cotizaciones (cliente_id);
            CREATE INDEX IF NOT EXISTS IX_cotizaciones_sucursal_id ON cotizaciones (sucursal_id);
            CREATE INDEX IF NOT EXISTS IX_cotizaciones_vendedor_id ON cotizaciones (vendedor_id);

            CREATE TABLE IF NOT EXISTS cotizacion_detalle (
                id serial PRIMARY KEY,
                cotizacion_id integer NOT NULL REFERENCES cotizaciones(id) ON DELETE CASCADE,
                producto_id integer NOT NULL REFERENCES productos(id) ON DELETE CASCADE,
                unidad_medida_id integer NOT NULL,
                unidad_nombre varchar(20) NOT NULL DEFAULT 'Litro',
                cantidad numeric(12,4) NOT NULL DEFAULT 0,
                precio_unitario numeric(12,4) NOT NULL DEFAULT 0,
                descuento numeric(12,2) NOT NULL DEFAULT 0,
                importe numeric(12,2) NOT NULL DEFAULT 0,
                lote_id integer
            );
            CREATE INDEX IF NOT EXISTS IX_cotizacion_detalle_cotizacion_id ON cotizacion_detalle (cotizacion_id);
            CREATE INDEX IF NOT EXISTS IX_cotizacion_detalle_producto_id ON cotizacion_detalle (producto_id);

            CREATE TABLE IF NOT EXISTS ordenes_venta (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL REFERENCES sucursales(id) ON DELETE CASCADE,
                cliente_id integer NOT NULL REFERENCES clientes(id) ON DELETE CASCADE,
                vendedor_id integer REFERENCES usuarios(id) ON DELETE RESTRICT,
                fecha_emision timestamptz NOT NULL DEFAULT now(),
                folio_interno varchar(40),
                notas text,
                estado varchar(20) NOT NULL DEFAULT 'Borrador',
                cotizacion_id integer REFERENCES cotizaciones(id) ON DELETE RESTRICT,
                factura_id integer REFERENCES facturas(id) ON DELETE SET NULL,
                usuario_id integer NOT NULL DEFAULT 0,
                subtotal numeric(12,2) NOT NULL DEFAULT 0,
                iva numeric(12,2) NOT NULL DEFAULT 0,
                total numeric(12,2) NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_ordenes_venta_cliente_id ON ordenes_venta (cliente_id);
            CREATE INDEX IF NOT EXISTS IX_ordenes_venta_cotizacion_id ON ordenes_venta (cotizacion_id);
            CREATE INDEX IF NOT EXISTS IX_ordenes_venta_sucursal_id ON ordenes_venta (sucursal_id);
            CREATE INDEX IF NOT EXISTS IX_ordenes_venta_vendedor_id ON ordenes_venta (vendedor_id);

            CREATE TABLE IF NOT EXISTS orden_venta_detalle (
                id serial PRIMARY KEY,
                orden_venta_id integer NOT NULL REFERENCES ordenes_venta(id) ON DELETE CASCADE,
                producto_id integer NOT NULL REFERENCES productos(id) ON DELETE CASCADE,
                unidad_medida_id integer NOT NULL,
                unidad_nombre varchar(20) NOT NULL DEFAULT 'Litro',
                cantidad numeric(12,4) NOT NULL DEFAULT 0,
                precio_unitario numeric(12,4) NOT NULL DEFAULT 0,
                descuento numeric(12,2) NOT NULL DEFAULT 0,
                importe numeric(12,2) NOT NULL DEFAULT 0,
                lote_id integer
            );
            CREATE INDEX IF NOT EXISTS IX_orden_venta_detalle_orden_venta_id ON orden_venta_detalle (orden_venta_id);
            CREATE INDEX IF NOT EXISTS IX_orden_venta_detalle_producto_id ON orden_venta_detalle (producto_id);
            """);

        // Capa B: ligue orden → factura. Tolera tablas creadas por la versión ANTERIOR del patch
        // (CREATE TABLE no altera tablas existentes) y tablas creadas por EF con EnsureCreated
        // (ya traen la columna): ADD COLUMN IF NOT EXISTS es no-op en ambos casos.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE ordenes_venta
                ADD COLUMN IF NOT EXISTS factura_id integer REFERENCES facturas(id) ON DELETE SET NULL;
            CREATE INDEX IF NOT EXISTS IX_ordenes_venta_factura_id ON ordenes_venta (factura_id);
            """);

        // Orden manual: la columna cotizacion_id era NOT NULL (0 = manual) y la FK rechazaba el 0,
        // por lo que NINGUNA orden manual podía crearse. Ahora null = manual; DROP NOT NULL es no-op
        // en tablas creadas por la versión nueva del patch o por EnsureCreated con el modelo actual.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE ordenes_venta ALTER COLUMN cotizacion_id DROP NOT NULL;");

        // CHECK de los enums (estado de cotización y de orden), tolerante a BD previas.
        // Valores leídos del enum para no duplicarlos a mano (mismo patrón que ComisionPatch).
        // El de ordenes_venta se RECREA siempre (DROP IF EXISTS + ADD): el estado 'Facturada' es
        // nuevo y un constraint creado por la versión anterior no lo incluiría (rechazaría el
        // update de facturación con un error de check constraint).
        var estadosCot = SqlLista(Enum.GetNames<EstadoCotizacion>());
        var estadosOrd = SqlLista(Enum.GetNames<EstadoOrdenVenta>());
        await db.Database.ExecuteSqlRawAsync($"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cotizaciones_estado') THEN
                    ALTER TABLE cotizaciones ADD CONSTRAINT ck_cotizaciones_estado
                        CHECK (estado IN ({estadosCot}));
                END IF;
            END $$;
            ALTER TABLE ordenes_venta DROP CONSTRAINT IF EXISTS ck_ordenes_estado;
            ALTER TABLE ordenes_venta ADD CONSTRAINT ck_ordenes_estado
                CHECK (estado IN ({estadosOrd}));
            """);
    }

    /// <summary>Convierte ["Borrador","Expirada"] en 'Borrador','Expirada' para dentro de un IN de SQL.</summary>
    private static string SqlLista(string[] valores) =>
        string.Join(", ", valores.Select(v => $"'{v}'"));
}

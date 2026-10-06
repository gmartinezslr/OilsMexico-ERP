using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche fiscal y caja (NC electrónica, cobros PPD + REP, cortes por turno).
/// Se invoca desde SchemaPatch.AplicarAsync; idempotente con IF NOT EXISTS.
/// </summary>
public static class FiscalCajaPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS notas_credito (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                folio_interno varchar(30) NOT NULL UNIQUE,
                factura_origen_id integer NOT NULL REFERENCES facturas(id),
                cliente_id integer NOT NULL,
                uuid_factura_origen uuid,
                uuid_sat uuid UNIQUE,
                fecha_emision timestamptz NOT NULL DEFAULT now(),
                subtotal numeric(12,2) NOT NULL DEFAULT 0,
                iva numeric(12,2) NOT NULL DEFAULT 0,
                total numeric(12,2) NOT NULL DEFAULT 0,
                motivo varchar(20) NOT NULL DEFAULT 'Devolucion',
                uso_cfdi varchar(5) NOT NULL DEFAULT 'G02',
                tipo_relacion varchar(2) NOT NULL DEFAULT '01',
                xml_sellado text,
                sello_digital text,
                cadena_original text,
                estado varchar(20) NOT NULL DEFAULT 'Pendiente',
                motivo_cancelacion varchar(300),
                repuso_stock boolean NOT NULL DEFAULT false,
                observaciones text,
                usuario_id integer NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS nota_credito_detalle (
                id serial PRIMARY KEY,
                nota_credito_id integer NOT NULL REFERENCES notas_credito(id) ON DELETE CASCADE,
                producto_id integer NOT NULL,
                unidad_nombre varchar(20) NOT NULL DEFAULT 'Litro',
                cantidad numeric(12,2) NOT NULL DEFAULT 0,
                precio_unitario numeric(12,2) NOT NULL DEFAULT 0,
                importe numeric(12,2) NOT NULL DEFAULT 0,
                lote_id integer
            );
            CREATE INDEX IF NOT EXISTS ix_nc_origen ON notas_credito (factura_origen_id);
            CREATE INDEX IF NOT EXISTS ix_nc_sucursal ON notas_credito (sucursal_id);
            """);
    }
}

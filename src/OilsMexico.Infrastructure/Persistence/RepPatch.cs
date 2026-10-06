using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>Parche cobros PPD + REP (Tipo P). Idempotente.</summary>
public static class RepPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS venta_cobros (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                factura_id integer NOT NULL REFERENCES facturas(id),
                cliente_id integer NOT NULL,
                monto numeric(12,2) NOT NULL,
                forma_pago_sat varchar(2) NOT NULL DEFAULT '01',
                fecha_pago_utc timestamptz NOT NULL DEFAULT now(),
                referencia varchar(100),
                usuario_id integer NOT NULL DEFAULT 0,
                complemento_pago_id integer,
                creado_utc timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS complementos_pago (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                folio_interno varchar(30) NOT NULL UNIQUE,
                cliente_id integer NOT NULL,
                fecha_emision timestamptz NOT NULL DEFAULT now(),
                total numeric(12,2) NOT NULL DEFAULT 0,
                uuid_sat uuid UNIQUE,
                xml_sellado text,
                sello_digital text,
                cadena_original text,
                estado varchar(20) NOT NULL DEFAULT 'Pendiente',
                usuario_id integer NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS complemento_pago_detalle (
                id serial PRIMARY KEY,
                complemento_pago_id integer NOT NULL REFERENCES complementos_pago(id) ON DELETE CASCADE,
                factura_id integer NOT NULL REFERENCES facturas(id),
                venta_cobro_id integer NOT NULL,
                uuid_factura uuid,
                folio_factura varchar(30) NOT NULL DEFAULT '',
                num_parcialidad integer NOT NULL DEFAULT 1,
                imp_saldo_ant numeric(12,2) NOT NULL DEFAULT 0,
                imp_pagado numeric(12,2) NOT NULL DEFAULT 0,
                imp_saldo_insoluto numeric(12,2) NOT NULL DEFAULT 0,
                moneda varchar(3) NOT NULL DEFAULT 'MXN'
            );
            CREATE INDEX IF NOT EXISTS ix_venta_cobros_factura ON venta_cobros (factura_id);
            CREATE INDEX IF NOT EXISTS ix_rep_sucursal ON complementos_pago (sucursal_id);
            """);
        // FK cobro -> REP (tolerante: solo si ambas tablas existen y el constraint falta).
        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_venta_cobros_rep') THEN
                    ALTER TABLE venta_cobros
                        ADD CONSTRAINT fk_venta_cobros_rep FOREIGN KEY (complemento_pago_id)
                        REFERENCES complementos_pago(id) ON DELETE SET NULL NOT VALID;
                END IF;
            END $$;
            """);
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche idempotente del módulo de conciliación bancaria: cuentas y extracto.
/// Las tablas se crean aquí (el proyecto no usa migraciones EF); los CHECK de los enums se
/// agregan leyendo <c>Enum.GetNames</c> para no duplicar los valores, igual que ComisionPatch.
/// Se invoca desde SchemaPatch.AplicarAsync.
/// </summary>
public static class BancosPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cuentas_bancarias (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                banco varchar(60) NOT NULL,
                alias varchar(80) NOT NULL,
                numero_cuenta varchar(30),
                clabe varchar(18),
                moneda varchar(3) NOT NULL DEFAULT 'MXN',
                saldo_inicial numeric(12,2) NOT NULL DEFAULT 0,
                activa boolean NOT NULL DEFAULT true
            );
            CREATE INDEX IF NOT EXISTS ix_cuentas_bancarias_sucursal ON cuentas_bancarias (sucursal_id);

            CREATE TABLE IF NOT EXISTS movimientos_bancarios (
                id serial PRIMARY KEY,
                cuenta_bancaria_id integer NOT NULL REFERENCES cuentas_bancarias(id) ON DELETE CASCADE,
                fecha timestamptz NOT NULL DEFAULT now(),
                concepto varchar(200) NOT NULL DEFAULT '',
                referencia varchar(100),
                naturaleza varchar(10) NOT NULL DEFAULT 'Abono',
                monto numeric(12,2) NOT NULL DEFAULT 0,
                estado varchar(15) NOT NULL DEFAULT 'Pendiente',
                venta_cobro_id integer,
                compra_pago_id integer,
                conciliado_utc timestamptz,
                conciliado_por_usuario_id integer,
                notas text,
                creado_utc timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_mov_bancarios_cuenta ON movimientos_bancarios (cuenta_bancaria_id);
            CREATE INDEX IF NOT EXISTS ix_mov_bancarios_cuenta_estado ON movimientos_bancarios (cuenta_bancaria_id, estado);
            """);

        // FKs de conciliación y CHECK de enums. Se añaden aparte por tolerancia a BD previas:
        // sólo si la columna existe y no tiene YA una FK (en BD nueva EF las crea con otro nombre),
        // para no romper el arranque ni dejar FKs duplicadas sobre la misma columna.
        var naturalezas = SqlLista(Enum.GetNames<NaturalezaMovimiento>());
        var estados = SqlLista(Enum.GetNames<EstadoConciliacion>());
        await db.Database.ExecuteSqlRawAsync($"""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'movimientos_bancarios'
                             AND column_name = 'venta_cobro_id')
                   AND NOT EXISTS (
                        SELECT 1 FROM pg_constraint c
                        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY(c.conkey)
                        WHERE c.contype = 'f'
                          AND c.conrelid = 'movimientos_bancarios'::regclass
                          AND a.attname = 'venta_cobro_id') THEN
                    ALTER TABLE movimientos_bancarios
                        ADD CONSTRAINT fk_mov_ban_venta_cobro FOREIGN KEY (venta_cobro_id)
                        REFERENCES venta_cobros(id) ON DELETE RESTRICT NOT VALID;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'movimientos_bancarios'
                             AND column_name = 'compra_pago_id')
                   AND NOT EXISTS (
                        SELECT 1 FROM pg_constraint c
                        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY(c.conkey)
                        WHERE c.contype = 'f'
                          AND c.conrelid = 'movimientos_bancarios'::regclass
                          AND a.attname = 'compra_pago_id') THEN
                    ALTER TABLE movimientos_bancarios
                        ADD CONSTRAINT fk_mov_ban_compra_pago FOREIGN KEY (compra_pago_id)
                        REFERENCES compra_pagos(id) ON DELETE RESTRICT NOT VALID;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_mov_ban_naturaleza') THEN
                    ALTER TABLE movimientos_bancarios ADD CONSTRAINT ck_mov_ban_naturaleza
                        CHECK (naturaleza IN ({naturalezas}));
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_mov_ban_estado') THEN
                    ALTER TABLE movimientos_bancarios ADD CONSTRAINT ck_mov_ban_estado
                        CHECK (estado IN ({estados}));
                END IF;
                -- El monto de un renglón del extracto nunca es negativo.
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_mov_ban_monto_no_negativo') THEN
                    ALTER TABLE movimientos_bancarios ADD CONSTRAINT ck_mov_ban_monto_no_negativo
                        CHECK (monto >= 0);
                END IF;
            END $$;
            """);
    }

    /// <summary>Convierte [\"Abono\",\"Cargo\"] en 'Abono','Cargo' para dentro de un IN de SQL.</summary>
    private static string SqlLista(string[] valores) =>
        string.Join(", ", valores.Select(v => $"'{v}'"));
}

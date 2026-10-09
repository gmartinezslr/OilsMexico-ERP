using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche idempotente del módulo de comisiones (fase 3): cuotas y cierre congelado.
/// Las tablas se crean aquí (el proyecto no usa migraciones EF); los CHECK de los enums se
/// agregan en el segundo paso leyendo <c>Enum.GetNames</c> para no duplicar los valores.
/// </summary>
public static class ComisionPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cuotas_vendedor (
                id serial PRIMARY KEY,
                vendedor_id integer NOT NULL,
                anio integer NOT NULL,
                mes integer NOT NULL,
                meta_dinero numeric(12,2),
                meta_litros numeric(12,2),
                operador varchar(20) NOT NULL DEFAULT 'SoloDinero',
                accion_incumplimiento varchar(20) NOT NULL DEFAULT 'CeroComision',
                monto_pago_minimo numeric(12,2) NOT NULL DEFAULT 0,
                notas varchar,
                creado_utc timestamptz NOT NULL DEFAULT now(),
                actualizado_utc timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_cuotas_vendedor_periodo
                ON cuotas_vendedor (vendedor_id, anio, mes);
            CREATE INDEX IF NOT EXISTS ix_cuotas_periodo ON cuotas_vendedor (anio, mes);

            CREATE TABLE IF NOT EXISTS comisiones_historial (
                id serial PRIMARY KEY,
                vendedor_id integer NOT NULL,
                anio integer NOT NULL,
                mes integer NOT NULL,
                estado varchar(20) NOT NULL DEFAULT 'Borrador',
                dinero_real numeric(12,2) NOT NULL DEFAULT 0,
                litros_reales numeric(12,2) NOT NULL DEFAULT 0,
                cobrado_bruto numeric(12,2) NOT NULL DEFAULT 0,
                facturas_cobradas integer NOT NULL DEFAULT 0,
                meta_dinero numeric(12,2),
                meta_litros numeric(12,2),
                operador varchar(20) NOT NULL DEFAULT 'SoloDinero',
                accion_incumplimiento varchar(20) NOT NULL DEFAULT 'CeroComision',
                monto_pago_minimo numeric(12,2) NOT NULL DEFAULT 0,
                pct_dinero numeric(8,2) NOT NULL DEFAULT 0,
                pct_litros numeric(8,2) NOT NULL DEFAULT 0,
                cumplio_meta boolean NOT NULL DEFAULT false,
                detalle_evaluacion varchar(300),
                comision_base numeric(12,2) NOT NULL DEFAULT 0,
                comision_bono numeric(12,2) NOT NULL DEFAULT 0,
                comision_final numeric(12,2) NOT NULL DEFAULT 0,
                aplico_pago_minimo boolean NOT NULL DEFAULT false,
                productos_sin_tabulador varchar,
                desglose_json varchar,
                calculado_utc timestamptz NOT NULL DEFAULT now(),
                cerrado_por_usuario_id integer,
                cerrado_utc timestamptz,
                pagado_utc timestamptz,
                notas varchar
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_comisiones_historial_periodo
                ON comisiones_historial (vendedor_id, anio, mes);
            CREATE INDEX IF NOT EXISTS ix_comisiones_periodo
                ON comisiones_historial (anio, mes, estado);

            -- Tabulador de comisiones por presentación: DEFAULT 0 = sin configurar (no comisiona),
            -- para que un SKU nuevo no rompa el alta ni herede un % inventado.
            ALTER TABLE unidades_medida ADD COLUMN IF NOT EXISTS porc_comision_base numeric(5,2) NOT NULL DEFAULT 0;
            ALTER TABLE unidades_medida ADD COLUMN IF NOT EXISTS porc_comision_bono numeric(5,2) NOT NULL DEFAULT 0;
            """);

        await AplicarConstraintsAsync(db);
    }

    /// <summary>
    /// CHECKs de integridad. Se generan desde los enums para que no haya que mantener dos
    /// listas, y se envuelven en DO $$ + IF NOT EXISTS para ser idempotentes.
    /// </summary>
    private static Task AplicarConstraintsAsync(ErpDbContext db)
    {
        var operadores = SqlLista(Enum.GetNames<OperadorLogico>());
        var acciones = SqlLista(Enum.GetNames<AccionIncumplimiento>());
        var estados = SqlLista(Enum.GetNames<EstadoComision>());

        return db.Database.ExecuteSqlRawAsync($"""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cuotas_operador') THEN
                    ALTER TABLE cuotas_vendedor ADD CONSTRAINT ck_cuotas_operador
                        CHECK (operador IN ({operadores}));
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cuotas_accion') THEN
                    ALTER TABLE cuotas_vendedor ADD CONSTRAINT ck_cuotas_accion
                        CHECK (accion_incumplimiento IN ({acciones}));
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cuotas_periodo_valido') THEN
                    ALTER TABLE cuotas_vendedor ADD CONSTRAINT ck_cuotas_periodo_valido
                        CHECK (mes BETWEEN 1 AND 12 AND anio BETWEEN 2000 AND 2100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cuotas_metas_no_negativas') THEN
                    ALTER TABLE cuotas_vendedor ADD CONSTRAINT ck_cuotas_metas_no_negativas
                        CHECK ((meta_dinero IS NULL OR meta_dinero >= 0)
                           AND (meta_litros IS NULL OR meta_litros >= 0)
                           AND monto_pago_minimo >= 0);
                END IF;
                -- Una cuota sin metas activas es basura: nunca podría cumplirse.
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cuotas_al_menos_una_meta') THEN
                    ALTER TABLE cuotas_vendedor ADD CONSTRAINT ck_cuotas_al_menos_una_meta
                        CHECK (meta_dinero IS NOT NULL OR meta_litros IS NOT NULL);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_comisiones_estado') THEN
                    ALTER TABLE comisiones_historial ADD CONSTRAINT ck_comisiones_estado
                        CHECK (estado IN ({estados}));
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_comisiones_operador') THEN
                    ALTER TABLE comisiones_historial ADD CONSTRAINT ck_comisiones_operador
                        CHECK (operador IN ({operadores}));
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_comisiones_periodo_valido') THEN
                    ALTER TABLE comisiones_historial ADD CONSTRAINT ck_comisiones_periodo_valido
                        CHECK (mes BETWEEN 1 AND 12 AND anio BETWEEN 2000 AND 2100);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_tabulador_bono_no_menor_base') THEN
                    -- El % de bono nunca por debajo del base: sería un incentivo invertido
                    -- (cumplir la meta pagaría menos que no cumplirla).
                    ALTER TABLE unidades_medida ADD CONSTRAINT ck_tabulador_bono_no_menor_base
                        CHECK (porc_comision_bono >= porc_comision_base);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_tabulador_porc_rango') THEN
                    ALTER TABLE unidades_medida ADD CONSTRAINT ck_tabulador_porc_rango
                        CHECK (porc_comision_base BETWEEN 0 AND 100
                           AND porc_comision_bono BETWEEN 0 AND 100);
                END IF;
            END $$;
            """);
    }

    /// <summary>Convierte ["Y","O"] en 'Y','O' para dentro de un IN de SQL.</summary>
    private static string SqlLista(string[] valores) =>
        string.Join(", ", valores.Select(v => $"'{v}'"));
}

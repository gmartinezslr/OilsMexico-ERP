using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche idempotente del módulo de gastos operativos: tabla gastos.
/// Se invoca desde SchemaPatch.AplicarAsync. El CHECK de categorías se deriva
/// de <see cref="CategoriaGasto"/> vía Enum.GetNames (mismo patrón que BancosPatch).
/// </summary>
public static class GastosPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS gastos (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                fecha timestamptz NOT NULL DEFAULT now(),
                categoria varchar(30) NOT NULL DEFAULT 'Otros',
                concepto varchar(200) NOT NULL DEFAULT '',
                importe numeric(12,2) NOT NULL DEFAULT 0,
                forma_pago_sat varchar(2) NOT NULL DEFAULT '01',
                comprobante varchar(100),
                corte_caja_id integer,
                notas text,
                usuario_id integer NOT NULL DEFAULT 0,
                creado_utc timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_gastos_sucursal_fecha ON gastos (sucursal_id, fecha);
            CREATE INDEX IF NOT EXISTS ix_gastos_categoria ON gastos (categoria);
            """);

        // FK al corte (tolerante a BD previas) + CHECK de categorías + importe no negativo.
        var categorias = SqlLista(Enum.GetNames<CategoriaGasto>());
        await db.Database.ExecuteSqlRawAsync($"""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'gastos'
                             AND column_name = 'corte_caja_id')
                   AND NOT EXISTS (
                        SELECT 1 FROM pg_constraint c
                        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY(c.conkey)
                        WHERE c.contype = 'f'
                          AND c.conrelid = 'gastos'::regclass
                          AND a.attname = 'corte_caja_id') THEN
                    ALTER TABLE gastos
                        ADD CONSTRAINT fk_gastos_corte FOREIGN KEY (corte_caja_id)
                        REFERENCES cortes_caja(id) ON DELETE SET NULL NOT VALID;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_gastos_categoria') THEN
                    ALTER TABLE gastos ADD CONSTRAINT ck_gastos_categoria
                        CHECK (categoria IN ({categorias}));
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_gastos_importe_positivo') THEN
                    ALTER TABLE gastos ADD CONSTRAINT ck_gastos_importe_positivo
                        CHECK (importe > 0);
                END IF;
            END $$;
            """);
    }

    /// <summary>Convierte ["Combustible","Taller"] en 'Combustible','Taller' para dentro de un IN de SQL.</summary>
    private static string SqlLista(string[] valores) =>
        string.Join(", ", valores.Select(v => $"'{v}'"));
}

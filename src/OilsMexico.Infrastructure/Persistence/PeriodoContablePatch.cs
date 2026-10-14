using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche idempotente del cierre de periodo contable (§1.3): tabla periodos_contables.
/// Se invoca desde SchemaPatch.AplicarAsync. El índice único (sucursal_id, anio, mes)
/// es a la vez la bandera de periodo cerrado y el guard de concurrencia del cierre.
/// </summary>
public static class PeriodoContablePatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS periodos_contables (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                anio integer NOT NULL,
                mes integer NOT NULL,
                usuario_cierre_id integer NOT NULL DEFAULT 0,
                cerrado_utc timestamptz NOT NULL DEFAULT now(),
                notas varchar(400)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_periodos_contables_sucursal_periodo
                ON periodos_contables (sucursal_id, anio, mes);
            """);
    }
}
using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>Parche cortes de caja por turno. Idempotente.</summary>
public static class CajaPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cortes_caja (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                usuario_apertura_id integer NOT NULL,
                usuario_apertura_nombre varchar(100) NOT NULL DEFAULT '',
                usuario_cierre_id integer,
                fecha_apertura_utc timestamptz NOT NULL DEFAULT now(),
                fecha_cierre_utc timestamptz,
                fondo_inicial numeric(12,2) NOT NULL DEFAULT 0,
                total_efectivo_sistema numeric(12,2) NOT NULL DEFAULT 0,
                total_tarjeta_sistema numeric(12,2) NOT NULL DEFAULT 0,
                total_transfer_sistema numeric(12,2) NOT NULL DEFAULT 0,
                total_otros_sistema numeric(12,2) NOT NULL DEFAULT 0,
                total_ventas_sistema numeric(12,2) NOT NULL DEFAULT 0,
                num_ventas integer NOT NULL DEFAULT 0,
                efectivo_contado numeric(12,2) NOT NULL DEFAULT 0,
                diferencia numeric(12,2) NOT NULL DEFAULT 0,
                estado varchar(20) NOT NULL DEFAULT 'Abierto',
                observaciones text
            );
            CREATE INDEX IF NOT EXISTS ix_cortes_sucursal_estado ON cortes_caja (sucursal_id, estado);
            """);
    }
}

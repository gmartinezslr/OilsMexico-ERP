using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Esquema idempotente para contactos, medios y direcciones múltiples de clientes/proveedores.
/// También migra los campos planos existentes una sola vez por tercero, conservando así los datos
/// usados por versiones anteriores del ERP.
/// </summary>
public static class TerceroContactosPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE clientes ALTER COLUMN telefono TYPE varchar(40);
            ALTER TABLE clientes ALTER COLUMN email TYPE varchar(254);
            ALTER TABLE proveedores ALTER COLUMN telefono TYPE varchar(40);
            ALTER TABLE proveedores ALTER COLUMN email TYPE varchar(254);

            CREATE TABLE IF NOT EXISTS terceros_contactos (
                id serial PRIMARY KEY,
                cliente_id integer REFERENCES clientes(id) ON DELETE CASCADE,
                proveedor_id integer REFERENCES proveedores(id) ON DELETE CASCADE,
                nombre varchar(150) NOT NULL,
                puesto varchar(120),
                area varchar(120),
                es_principal boolean NOT NULL DEFAULT false,
                es_respaldo boolean NOT NULL DEFAULT false,
                puede_recibir_pedidos boolean NOT NULL DEFAULT false,
                puede_recibir_notificaciones boolean NOT NULL DEFAULT false,
                alcance_autorizacion varchar(500),
                horario varchar(200),
                notas varchar(500),
                CONSTRAINT ck_terceros_contactos_tercero
                    CHECK ((cliente_id IS NOT NULL AND proveedor_id IS NULL)
                        OR (cliente_id IS NULL AND proveedor_id IS NOT NULL))
            );
            CREATE TABLE IF NOT EXISTS terceros_correos (
                id serial PRIMARY KEY,
                cliente_id integer REFERENCES clientes(id) ON DELETE CASCADE,
                proveedor_id integer REFERENCES proveedores(id) ON DELETE CASCADE,
                contacto_tercero_id integer REFERENCES terceros_contactos(id) ON DELETE SET NULL,
                tipo varchar(60) NOT NULL DEFAULT 'Principal',
                direccion varchar(254) NOT NULL,
                es_principal boolean NOT NULL DEFAULT false,
                notas varchar(500),
                CONSTRAINT ck_terceros_correos_tercero
                    CHECK ((cliente_id IS NOT NULL AND proveedor_id IS NULL)
                        OR (cliente_id IS NULL AND proveedor_id IS NOT NULL))
            );
            CREATE TABLE IF NOT EXISTS terceros_telefonos (
                id serial PRIMARY KEY,
                cliente_id integer REFERENCES clientes(id) ON DELETE CASCADE,
                proveedor_id integer REFERENCES proveedores(id) ON DELETE CASCADE,
                contacto_tercero_id integer REFERENCES terceros_contactos(id) ON DELETE SET NULL,
                tipo varchar(60) NOT NULL DEFAULT 'Celular',
                numero varchar(40) NOT NULL,
                tiene_whatsapp boolean NOT NULL DEFAULT false,
                es_principal boolean NOT NULL DEFAULT false,
                horario varchar(200),
                notas varchar(500),
                CONSTRAINT ck_terceros_telefonos_tercero
                    CHECK ((cliente_id IS NOT NULL AND proveedor_id IS NULL)
                        OR (cliente_id IS NULL AND proveedor_id IS NOT NULL))
            );
            CREATE TABLE IF NOT EXISTS terceros_direcciones (
                id serial PRIMARY KEY,
                cliente_id integer REFERENCES clientes(id) ON DELETE CASCADE,
                proveedor_id integer REFERENCES proveedores(id) ON DELETE CASCADE,
                contacto_recibe_id integer REFERENCES terceros_contactos(id) ON DELETE SET NULL,
                tipo varchar(60) NOT NULL DEFAULT 'Principal',
                calle varchar(150),
                numero_exterior varchar(20),
                numero_interior varchar(20),
                colonia varchar(200),
                codigo_postal varchar(10),
                municipio varchar(150),
                estado varchar(100),
                ciudad varchar(150),
                pais varchar(60) NOT NULL DEFAULT 'México',
                es_predeterminada boolean NOT NULL DEFAULT false,
                horario varchar(200),
                instrucciones varchar(500),
                CONSTRAINT ck_terceros_direcciones_tercero
                    CHECK ((cliente_id IS NOT NULL AND proveedor_id IS NULL)
                        OR (cliente_id IS NULL AND proveedor_id IS NOT NULL))
            );

            CREATE UNIQUE INDEX IF NOT EXISTS uq_contactos_cliente_principal
                ON terceros_contactos (cliente_id, es_principal)
                WHERE es_principal = TRUE AND cliente_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_contactos_proveedor_principal
                ON terceros_contactos (proveedor_id)
                WHERE es_principal = TRUE AND proveedor_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_contactos_cliente_respaldo
                ON terceros_contactos (cliente_id)
                WHERE es_respaldo = TRUE AND cliente_id IS NOT NULL;

            CREATE INDEX IF NOT EXISTS "IX_terceros_correos_contacto_tercero_id"
                ON terceros_correos (contacto_tercero_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_correos_cliente_principal
                ON terceros_correos (cliente_id)
                WHERE es_principal = TRUE AND cliente_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_correos_proveedor_principal
                ON terceros_correos (proveedor_id)
                WHERE es_principal = TRUE AND proveedor_id IS NOT NULL;

            CREATE INDEX IF NOT EXISTS "IX_terceros_telefonos_contacto_tercero_id"
                ON terceros_telefonos (contacto_tercero_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_telefonos_cliente_principal
                ON terceros_telefonos (cliente_id)
                WHERE es_principal = TRUE AND cliente_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_telefonos_proveedor_principal
                ON terceros_telefonos (proveedor_id)
                WHERE es_principal = TRUE AND proveedor_id IS NOT NULL;

            CREATE INDEX IF NOT EXISTS "IX_terceros_direcciones_cliente_id" ON terceros_direcciones (cliente_id);
            CREATE INDEX IF NOT EXISTS "IX_terceros_direcciones_proveedor_id" ON terceros_direcciones (proveedor_id);
            CREATE INDEX IF NOT EXISTS "IX_terceros_direcciones_contacto_recibe_id"
                ON terceros_direcciones (contacto_recibe_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_direcciones_cliente_predeterminada_tipo
                ON terceros_direcciones (cliente_id, tipo)
                WHERE es_predeterminada = TRUE AND cliente_id IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_direcciones_proveedor_predeterminada_tipo
                ON terceros_direcciones (proveedor_id, tipo)
                WHERE es_predeterminada = TRUE AND proveedor_id IS NOT NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO terceros_correos (cliente_id, tipo, direccion, es_principal)
            SELECT c.id, 'Principal', c.email, TRUE
            FROM clientes c
            WHERE NULLIF(BTRIM(c.email), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM terceros_correos x WHERE x.cliente_id = c.id);

            INSERT INTO terceros_telefonos (cliente_id, tipo, numero, tiene_whatsapp, es_principal)
            SELECT c.id, 'Principal', c.telefono, FALSE, TRUE
            FROM clientes c
            WHERE NULLIF(BTRIM(c.telefono), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM terceros_telefonos x WHERE x.cliente_id = c.id);

            INSERT INTO terceros_direcciones
                (cliente_id, tipo, calle, numero_exterior, numero_interior, colonia, codigo_postal,
                 municipio, estado, ciudad, pais, es_predeterminada, instrucciones)
            SELECT c.id, 'Principal', c.calle, c.numero_exterior, c.numero_interior, c.colonia, c.codigo_postal,
                   c.municipio, c.estado, c.ciudad, c.pais, TRUE,
                   CASE WHEN NULLIF(BTRIM(c.calle), '') IS NULL
                             AND NULLIF(BTRIM(c.numero_exterior), '') IS NULL
                             AND NULLIF(BTRIM(c.numero_interior), '') IS NULL
                             AND NULLIF(BTRIM(c.colonia), '') IS NULL
                             AND NULLIF(BTRIM(c.codigo_postal), '') IS NULL
                             AND NULLIF(BTRIM(c.municipio), '') IS NULL
                             AND NULLIF(BTRIM(c.estado), '') IS NULL
                             AND NULLIF(BTRIM(c.ciudad), '') IS NULL
                        THEN c.direccion ELSE NULL END
            FROM clientes c
            WHERE (NULLIF(BTRIM(c.direccion), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.calle), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.numero_exterior), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.numero_interior), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.colonia), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.codigo_postal), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.municipio), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.estado), '') IS NOT NULL
                   OR NULLIF(BTRIM(c.ciudad), '') IS NOT NULL)
              AND NOT EXISTS (SELECT 1 FROM terceros_direcciones x WHERE x.cliente_id = c.id);

            INSERT INTO terceros_correos (proveedor_id, tipo, direccion, es_principal)
            SELECT p.id, 'Principal', p.email, TRUE
            FROM proveedores p
            WHERE NULLIF(BTRIM(p.email), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM terceros_correos x WHERE x.proveedor_id = p.id);

            INSERT INTO terceros_telefonos (proveedor_id, tipo, numero, tiene_whatsapp, es_principal)
            SELECT p.id, 'Principal', p.telefono, FALSE, TRUE
            FROM proveedores p
            WHERE NULLIF(BTRIM(p.telefono), '') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM terceros_telefonos x WHERE x.proveedor_id = p.id);

            INSERT INTO terceros_direcciones
                (proveedor_id, tipo, calle, numero_exterior, numero_interior, colonia, codigo_postal,
                 municipio, estado, ciudad, pais, es_predeterminada, instrucciones)
            SELECT p.id, 'Principal', p.calle, p.numero_exterior, p.numero_interior, p.colonia, p.codigo_postal,
                   p.municipio, p.estado, p.ciudad, p.pais, TRUE,
                   CASE WHEN NULLIF(BTRIM(p.calle), '') IS NULL
                             AND NULLIF(BTRIM(p.numero_exterior), '') IS NULL
                             AND NULLIF(BTRIM(p.numero_interior), '') IS NULL
                             AND NULLIF(BTRIM(p.colonia), '') IS NULL
                             AND NULLIF(BTRIM(p.codigo_postal), '') IS NULL
                             AND NULLIF(BTRIM(p.municipio), '') IS NULL
                             AND NULLIF(BTRIM(p.estado), '') IS NULL
                             AND NULLIF(BTRIM(p.ciudad), '') IS NULL
                        THEN p.direccion ELSE NULL END
            FROM proveedores p
            WHERE (NULLIF(BTRIM(p.direccion), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.calle), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.numero_exterior), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.numero_interior), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.colonia), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.codigo_postal), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.municipio), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.estado), '') IS NOT NULL
                   OR NULLIF(BTRIM(p.ciudad), '') IS NOT NULL)
              AND NOT EXISTS (SELECT 1 FROM terceros_direcciones x WHERE x.proveedor_id = p.id);
            """);
    }
}

using Microsoft.EntityFrameworkCore;

namespace OilsMexico.Infrastructure.Persistence;

/// <summary>
/// Parche de esquema idempotente para bases de datos ya existentes.
/// El proyecto no usa migraciones: EnsureCreatedAsync NO agrega tablas/columnas
/// a una BD que ya existe, así que este parche crea lo nuevo con IF NOT EXISTS.
/// Debe ejecutarse DESPUÉS de EnsureCreatedAsync y ANTES de cualquier consulta.
/// </summary>
public static class SchemaPatch
{
    public static async Task AplicarAsync(ErpDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS correo varchar(200);
            ALTER TABLE usuarios ALTER COLUMN pin_hash DROP NOT NULL;
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS password_hash varchar(128) NOT NULL DEFAULT '';
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS intentos_fallidos integer NOT NULL DEFAULT 0;
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS bloqueos_temporales integer NOT NULL DEFAULT 0;
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS bloqueado_hasta_utc timestamptz;
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS bloqueado_definitivamente boolean NOT NULL DEFAULT false;
            ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS sesion_token varchar(64);
            ALTER TABLE usuarios ALTER COLUMN pin_hash DROP NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_usuarios_correo ON usuarios (lower(correo)) WHERE correo IS NOT NULL;
            """);

        // Catálogo SEPOMEX (15 columnas del TXT oficial de Correos de México).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS codigos_postales (
                id serial PRIMARY KEY,
                codigo_postal varchar(10) NOT NULL,
                asentamiento varchar(200) NOT NULL,
                tipo_asentamiento varchar(60) NOT NULL,
                municipio varchar(150) NOT NULL,
                estado varchar(100) NOT NULL,
                ciudad varchar(150),
                dcp varchar(150),
                estado_id varchar(10),
                oficina varchar(10),
                ccp varchar(10),
                tipo_asentamiento_id varchar(10),
                municipio_id varchar(10),
                asentamiento_id varchar(10),
                zona varchar(20),
                ciudad_id varchar(10)
            );
            CREATE INDEX IF NOT EXISTS ix_codigos_postales_cp ON codigos_postales (codigo_postal);
            CREATE INDEX IF NOT EXISTS ix_codigos_postales_asentamiento ON codigos_postales (asentamiento);
            CREATE INDEX IF NOT EXISTS ix_codigos_postales_estado ON codigos_postales (estado);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_codigos_postales_cp_asentamiento
                ON codigos_postales (codigo_postal, asentamiento_id);
            """);

        // Datos de la sucursal: datos fiscales del emisor CFDI 4.0 + dirección desglosada.
        // Columnas anulables para no romper filas existentes (el modelo las declara string?).
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS razon_social varchar(150);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS regimen_fiscal varchar(10);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS codigo_postal varchar(10);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS calle varchar(150);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS numero_exterior varchar(20);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS numero_interior varchar(20);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS colonia varchar(200);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS municipio varchar(150);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS estado varchar(100);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS ciudad varchar(150);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS pais varchar(60);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS telefono varchar(20);
            ALTER TABLE sucursales ADD COLUMN IF NOT EXISTS email varchar(100);
            CREATE INDEX IF NOT EXISTS ix_sucursales_cp ON sucursales (codigo_postal);
            """);

        // Productos: costo unitario y características completas de lista de precios (Excel).
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_tables WHERE tablename = 'productos') THEN
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS precio_costo numeric(12,2) NOT NULL DEFAULT 0;
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS categoria varchar(50);
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS sku_anterior varchar(50);
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS sae varchar(20);
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS especificacion varchar(100);
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS piezas_por_caja integer NOT NULL DEFAULT 1;
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS precio_lista numeric(12,2) NOT NULL DEFAULT 0;
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS precio_lp_oro_con_iva numeric(12,2) NOT NULL DEFAULT 0;
                    ALTER TABLE productos ADD COLUMN IF NOT EXISTS precio_unitario numeric(12,2) NOT NULL DEFAULT 0;
                    CREATE INDEX IF NOT EXISTS ix_productos_sku_anterior ON productos (sku_anterior);
                    CREATE INDEX IF NOT EXISTS ix_productos_categoria ON productos (categoria);
                END IF;
            END $$;
        ");

        // Facturación CFDI: motivo de cancelación (obligatorio ante el SAT).
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE facturas ADD COLUMN IF NOT EXISTS motivo_cancelacion varchar(300);
            """);

        // Proveedores con dirección desglosada.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS proveedores (
                id serial PRIMARY KEY,
                nombre varchar(150) NOT NULL,
                rfc varchar(13) DEFAULT 'XAXX010101000',
                telefono varchar(20),
                email varchar(100),
                calle varchar(150),
                numero_exterior varchar(20),
                numero_interior varchar(20),
                colonia varchar(200),
                codigo_postal varchar(10),
                municipio varchar(150),
                estado varchar(100),
                ciudad varchar(150),
                pais varchar(60) DEFAULT 'México',
                direccion text,
                activo boolean DEFAULT true
            );
            CREATE INDEX IF NOT EXISTS ix_proveedores_cp ON proveedores (codigo_postal);
            """);

        // Compras / Recepción / CxP (ciclo completo orden → recepción → pagos).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS compras (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                folio_interno varchar(30) NOT NULL,
                proveedor_id integer NOT NULL,
                folio_proveedor varchar(50),
                fecha_emision timestamptz NOT NULL DEFAULT now(),
                subtotal numeric(12,2) NOT NULL DEFAULT 0,
                iva numeric(12,2) NOT NULL DEFAULT 0,
                total numeric(12,2) NOT NULL DEFAULT 0,
                estado varchar(20) NOT NULL DEFAULT 'Borrador',
                estado_pago varchar(20) NOT NULL DEFAULT 'Pendiente',
                monto_pagado numeric(12,2) NOT NULL DEFAULT 0,
                notas text,
                usuario_id integer NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS compra_detalle (
                id serial PRIMARY KEY,
                compra_id integer NOT NULL REFERENCES compras(id) ON DELETE CASCADE,
                producto_id integer NOT NULL,
                unidad_medida_id integer,
                unidad_nombre varchar(20) NOT NULL DEFAULT 'Litro',
                cantidad numeric(12,4) NOT NULL DEFAULT 0,
                factor_conversion numeric(10,4) NOT NULL DEFAULT 1,
                cantidad_recibida numeric(12,4) NOT NULL DEFAULT 0,
                litros_recibidos numeric(12,2) NOT NULL DEFAULT 0,
                costo_unitario numeric(12,2) NOT NULL DEFAULT 0,
                numero_lote varchar(50),
                fecha_caducidad date
            );
            CREATE TABLE IF NOT EXISTS compra_pagos (
                id serial PRIMARY KEY,
                compra_id integer NOT NULL REFERENCES compras(id) ON DELETE CASCADE,
                monto numeric(12,2) NOT NULL,
                forma_pago varchar(2) NOT NULL DEFAULT '03',
                referencia varchar(100),
                fecha_utc timestamptz NOT NULL DEFAULT now(),
                usuario_id integer NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_compras_sucursal ON compras (sucursal_id);
            CREATE INDEX IF NOT EXISTS ix_compras_proveedor ON compras (proveedor_id);
            CREATE INDEX IF NOT EXISTS ix_compra_detalle_compra ON compra_detalle (compra_id);
            CREATE INDEX IF NOT EXISTS ix_compra_pagos_compra ON compra_pagos (compra_id);
            """);

        // Columnas nuevas de dirección en clientes (si la BD es anterior a SEPOMEX).
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS codigo_postal varchar(10);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS calle varchar(150);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS numero_exterior varchar(20);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS numero_interior varchar(20);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS colonia varchar(200);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS municipio varchar(150);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS estado varchar(100);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS ciudad varchar(150);
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS pais varchar(60) DEFAULT 'México';
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS activo boolean DEFAULT true;
            CREATE INDEX IF NOT EXISTS ix_clientes_cp ON clientes (codigo_postal);
            """);

        // Contabilidad básica: catálogo de cuentas, pólizas y detalles (tablas nuevas).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cuentas_contables (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                codigo varchar(20) NOT NULL,
                nombre varchar(120) NOT NULL,
                tipo integer NOT NULL DEFAULT 1,
                descripcion varchar(250),
                saldo_actual numeric(12,2) NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_cuentas_contables_sucursal ON cuentas_contables (sucursal_id);
            CREATE UNIQUE INDEX IF NOT EXISTS uq_cuentas_contables_sucursal_codigo
                ON cuentas_contables (sucursal_id, codigo);

            CREATE TABLE IF NOT EXISTS asientos_contables (
                id serial PRIMARY KEY,
                sucursal_id integer NOT NULL,
                usuario_id integer NOT NULL,
                fecha_utc timestamptz NOT NULL DEFAULT now(),
                tipo varchar(30),
                numeracion varchar(30),
                concepto varchar(200),
                notas varchar(600)
            );
            CREATE INDEX IF NOT EXISTS ix_asientos_contables_sucursal ON asientos_contables (sucursal_id);

            CREATE TABLE IF NOT EXISTS detalle_asientos (
                id serial PRIMARY KEY,
                asiento_contable_id integer NOT NULL,
                cuenta_id integer NOT NULL,
                tipo_movimiento integer NOT NULL,
                importe numeric(12,2) NOT NULL DEFAULT 0,
                descripcion varchar(250)
            );
            CREATE INDEX IF NOT EXISTS ix_detalle_asientos_asiento ON detalle_asientos (asiento_contable_id);
            CREATE INDEX IF NOT EXISTS ix_detalle_asientos_cuenta ON detalle_asientos (cuenta_id);
            """);

        // Atribución de vendedores (fase 1 del módulo de comisiones). Idempotente.
        //  - clientes.vendedor_id : dueño comercial ACTUAL (mutable). NULL = sin dueño (Público en general).
        //  - facturas.vendedor_id : snapshot INMUTABLE de quién vendió (crédito de comisión).
        //    Se crea nullable: el histórico NO tiene de dónde saberlo (Factura nunca guardó el usuario
        //    que vendió y el legado PHP tampoco tiene "vendedor"), así que NULL = "sin atribución
        //    conocida". NO se adivina: no se paga comisión sobre datos inventados. Las altas nuevas
        //    siempre asignan valor desde VentasService.RegistrarVentaAsync.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE clientes ADD COLUMN IF NOT EXISTS vendedor_id integer;
            ALTER TABLE facturas ADD COLUMN IF NOT EXISTS vendedor_id integer;
            CREATE INDEX IF NOT EXISTS ix_clientes_vendedor ON clientes (vendedor_id);
            CREATE INDEX IF NOT EXISTS ix_facturas_vendedor ON facturas (vendedor_id, fecha_emision);
            """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_clientes_vendedor') THEN
                    ALTER TABLE clientes ADD CONSTRAINT fk_clientes_vendedor
                        FOREIGN KEY (vendedor_id) REFERENCES usuarios(id) ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_facturas_vendedor') THEN
                    ALTER TABLE facturas ADD CONSTRAINT fk_facturas_vendedor
                        FOREIGN KEY (vendedor_id) REFERENCES usuarios(id) ON DELETE RESTRICT;
                END IF;
                -- Refuerza el invariante del hecho SÓLO si no hay histórico sin atribución:
                -- con 0 filas NULL (BD nueva o histórico ya atribuido) la columna pasa a NOT NULL;
                -- si quedan facturas viejas sin vendedor conocido se conserva nullable y NO se
                -- adivina el valor.
                IF NOT EXISTS (SELECT 1 FROM facturas WHERE vendedor_id IS NULL) THEN
                    ALTER TABLE facturas ALTER COLUMN vendedor_id SET NOT NULL;
                END IF;
            END $$;
            """);

        await FiscalCajaPatch.AplicarAsync(db);
        await RepPatch.AplicarAsync(db);
        await CajaPatch.AplicarAsync(db);
        await ComisionPatch.AplicarAsync(db);

        // CASH BASIS: las ventas PUE (contado) se consideran cobradas al momento de emitirse.
        // Se estampa el cobro faltante en el histórico PUE para que Estado de Cuentas, cortes y
        // comisiones compartan UNA sola fuente de "dinero entrado" (venta_cobros).
        // Idempotente: sólo facturas PUE sin NINGÚN cobro; canceladas y devoluciones quedan fuera.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO venta_cobros (sucursal_id, factura_id, cliente_id, monto, forma_pago_sat,
                                      fecha_pago_utc, referencia, usuario_id, creado_utc)
            SELECT f.sucursal_id, f.id, f.cliente_id, f.total, f.forma_pago_sat,
                   f.fecha_emision, 'PUE-CONTADO (backfill)', 0, now()
            FROM facturas f
            WHERE f.metodo_pago_sat = 'PUE'
              AND f.estado NOT IN ('Cancelada', 'Devolucion')
              AND f.total > 0
              AND NOT EXISTS (SELECT 1 FROM venta_cobros vc WHERE vc.factura_id = f.id);
            """);

        // Parámetros de configuración del sistema (clave/valor).
        // sesion_timeout_min: minutos de inactividad antes de cerrar la sesión (por defecto 5).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS configuracion (
                clave varchar(80) PRIMARY KEY,
                valor varchar(200) NOT NULL
            );
            INSERT INTO configuracion (clave, valor) VALUES ('sesion_timeout_min', '5')
            ON CONFLICT (clave) DO NOTHING;
            """);
    }
}

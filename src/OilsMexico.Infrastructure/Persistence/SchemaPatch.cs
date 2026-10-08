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

        await FiscalCajaPatch.AplicarAsync(db);
        await RepPatch.AplicarAsync(db);
        await CajaPatch.AplicarAsync(db);
    }
}

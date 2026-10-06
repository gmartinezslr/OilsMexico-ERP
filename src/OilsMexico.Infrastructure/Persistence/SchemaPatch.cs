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
    }
}

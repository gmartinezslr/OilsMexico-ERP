using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OilsMexico.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asientos_contables",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    numeracion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notas = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asientos_contables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "codigos_postales",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo_postal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    asentamiento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo_asentamiento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ciudad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    dcp = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    estado_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    oficina = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ccp = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tipo_asentamiento_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    municipio_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    asentamiento_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    zona = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ciudad_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codigos_postales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracion",
                columns: table => new
                {
                    clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion", x => x.clave);
                });

            migrationBuilder.CreateTable(
                name: "cuentas_contables",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    saldo_actual = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas_contables", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ImportacionParametros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacionParametros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportacionReglas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Patron = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false),
                    Prioridad = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacionReglas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "movimientos_inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: true),
                    cantidad_litros = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    referencia_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_inventario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "productos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sku = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    viscosidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo_base = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descripcion_tecnica = table.Column<string>(type: "text", nullable: true),
                    categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sku_anterior = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sae = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    especificacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    piezas_por_caja = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    precio_lista = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    precio_lp_oro_con_iva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    precio_venta = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    precio_mayoreo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    precio_costo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_productos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false, defaultValue: "XAXX010101000"),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    calle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    numero_exterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero_interior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    colonia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    codigo_postal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    pais = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "México"),
                    direccion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    situacion_fiscal = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proveedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sucursales",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    codigo_sucursal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    direccion = table.Column<string>(type: "text", nullable: false),
                    rfc_emisor = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    razon_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    regimen_fiscal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    codigo_postal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    calle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    numero_exterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero_interior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    colonia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    pais = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sucursales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "detalle_asientos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asiento_contable_id = table.Column<int>(type: "integer", nullable: false),
                    cuenta_id = table.Column<int>(type: "integer", nullable: false),
                    tipo_movimiento = table.Column<int>(type: "integer", nullable: false),
                    importe = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalle_asientos", x => x.id);
                    table.ForeignKey(
                        name: "FK_detalle_asientos_asientos_contables_asiento_contable_id",
                        column: x => x.asiento_contable_id,
                        principalTable: "asientos_contables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalle_asientos_cuentas_contables_cuenta_id",
                        column: x => x.cuenta_id,
                        principalTable: "cuentas_contables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReglasAsiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Evento = table.Column<string>(type: "text", nullable: false),
                    CuentaId = table.Column<int>(type: "integer", nullable: false),
                    Debe = table.Column<bool>(type: "boolean", nullable: false),
                    MontoOrigen = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasAsiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglasAsiento_cuentas_contables_CuentaId",
                        column: x => x.CuentaId,
                        principalTable: "cuentas_contables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventario_lotes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    numero_lote = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fecha_fabricacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_caducidad = table.Column<DateOnly>(type: "date", nullable: true),
                    cantidad_disponible = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    almacen_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventario_lotes", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventario_lotes_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unidades_medida",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_nombre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    factor_conversion = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    codigo_barra = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    porc_comision_base = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m),
                    porc_comision_bono = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidades_medida", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidades_medida_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolPermisos",
                columns: table => new
                {
                    RolId = table.Column<int>(type: "integer", nullable: false),
                    PermisoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolPermisos", x => new { x.RolId, x.PermisoId });
                    table.ForeignKey(
                        name: "FK_RolPermisos_Permisos_PermisoId",
                        column: x => x.PermisoId,
                        principalTable: "Permisos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolPermisos_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compras",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    folio_interno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    proveedor_id = table.Column<int>(type: "integer", nullable: false),
                    folio_proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    iva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado_pago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monto_pagado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    notas = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compras", x => x.id);
                    table.ForeignKey(
                        name: "FK_compras_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compras_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cortes_caja",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_apertura_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_apertura_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    usuario_cierre_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_apertura_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fondo_inicial = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_efectivo_sistema = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_tarjeta_sistema = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_transfer_sistema = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_otros_sistema = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_ventas_sistema = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    num_ventas = table.Column<int>(type: "integer", nullable: false),
                    efectivo_contado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    diferencia = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cortes_caja", x => x.id);
                    table.ForeignKey(
                        name: "FK_cortes_caja_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pin_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bloqueos_temporales = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bloqueado_hasta_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    bloqueado_definitivamente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sesion_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    dos_fa_activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    dos_fa_secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ultimo_cambio_password_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuarios_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_usuarios_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventario_sucursal",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: true),
                    stock_actual = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    stock_minimo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 5m),
                    actualizado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventario_sucursal", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventario_sucursal_inventario_lotes_lote_id",
                        column: x => x.lote_id,
                        principalTable: "inventario_lotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventario_sucursal_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventario_sucursal_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compra_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    compra_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_medida_id = table.Column<int>(type: "integer", nullable: true),
                    unidad_nombre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    factor_conversion = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    cantidad_recibida = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    litros_recibidos = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    numero_lote = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fecha_caducidad = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_detalle_compras_compra_id",
                        column: x => x.compra_id,
                        principalTable: "compras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compra_detalle_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compra_pagos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    compra_id = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    forma_pago = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecha_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_pagos", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_pagos_compras_compra_id",
                        column: x => x.compra_id,
                        principalTable: "compras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    detalle = table.Column<string>(type: "text", nullable: true),
                    anterior = table.Column<string>(type: "text", nullable: true),
                    nuevo = table.Column<string>(type: "text", nullable: true),
                    fecha_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_logs_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rfc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false, defaultValue: "XAXX010101000"),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    direccion = table.Column<string>(type: "text", nullable: true),
                    calle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    numero_exterior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero_interior = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    colonia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    municipio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    pais = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "México"),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    tipo_precio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "menudeo"),
                    regimen_fiscal = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    codigo_postal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vendedor_id = table.Column<int>(type: "integer", nullable: true),
                    situacion_fiscal = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.id);
                    table.ForeignKey(
                        name: "FK_clientes_usuarios_vendedor_id",
                        column: x => x.vendedor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comisiones_historial",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vendedor_id = table.Column<int>(type: "integer", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dinero_real = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    litros_reales = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    cobrado_bruto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    facturas_cobradas = table.Column<int>(type: "integer", nullable: false),
                    meta_dinero = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    meta_litros = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accion_incumplimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monto_pago_minimo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    pct_dinero = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    pct_litros = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    cumplio_meta = table.Column<bool>(type: "boolean", nullable: false),
                    detalle_evaluacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    comision_base = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    comision_bono = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    comision_final = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    aplico_pago_minimo = table.Column<bool>(type: "boolean", nullable: false),
                    productos_sin_tabulador = table.Column<string>(type: "text", nullable: true),
                    desglose_json = table.Column<string>(type: "text", nullable: true),
                    calculado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cerrado_por_usuario_id = table.Column<int>(type: "integer", nullable: true),
                    cerrado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pagado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comisiones_historial", x => x.id);
                    table.ForeignKey(
                        name: "FK_comisiones_historial_usuarios_vendedor_id",
                        column: x => x.vendedor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cuotas_vendedor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vendedor_id = table.Column<int>(type: "integer", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    meta_dinero = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    meta_litros = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    operador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accion_incumplimiento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monto_pago_minimo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actualizado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuotas_vendedor", x => x.id);
                    table.ForeignKey(
                        name: "FK_cuotas_vendedor_usuarios_vendedor_id",
                        column: x => x.vendedor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "password_histories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fecha_cambio_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_histories_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "complementos_pago",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    folio_interno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cliente_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    uuid_sat = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_sellado = table.Column<string>(type: "text", nullable: true),
                    sello_digital = table.Column<string>(type: "text", nullable: true),
                    cadena_original = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complementos_pago", x => x.id);
                    table.ForeignKey(
                        name: "FK_complementos_pago_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_complementos_pago_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "facturas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    folio_interno = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uuid_sat = table.Column<Guid>(type: "uuid", nullable: true),
                    cliente_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    iva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    xml_sellado = table.Column<string>(type: "text", nullable: true),
                    sello_digital = table.Column<string>(type: "text", nullable: true),
                    cadena_original = table.Column<string>(type: "text", nullable: true),
                    metodo_pago_sat = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    forma_pago_sat = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    uso_cfdi = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    vendedor_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas", x => x.id);
                    table.ForeignKey(
                        name: "FK_facturas_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_facturas_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_facturas_usuarios_vendedor_id",
                        column: x => x.vendedor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "complemento_pago_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    complemento_pago_id = table.Column<int>(type: "integer", nullable: false),
                    factura_id = table.Column<int>(type: "integer", nullable: false),
                    venta_cobro_id = table.Column<int>(type: "integer", nullable: false),
                    uuid_factura = table.Column<Guid>(type: "uuid", nullable: true),
                    folio_factura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    num_parcialidad = table.Column<int>(type: "integer", nullable: false),
                    imp_saldo_ant = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    imp_pagado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    imp_saldo_insoluto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complemento_pago_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_complemento_pago_detalle_complementos_pago_complemento_pago~",
                        column: x => x.complemento_pago_id,
                        principalTable: "complementos_pago",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_complemento_pago_detalle_facturas_factura_id",
                        column: x => x.factura_id,
                        principalTable: "facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factura_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    factura_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_medida_id = table.Column<int>(type: "integer", nullable: true),
                    unidad_nombre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    litros_descontados = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factura_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_factura_detalle_facturas_factura_id",
                        column: x => x.factura_id,
                        principalTable: "facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_factura_detalle_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notas_credito",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    folio_interno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    factura_origen_id = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<int>(type: "integer", nullable: false),
                    uuid_factura_origen = table.Column<Guid>(type: "uuid", nullable: true),
                    uuid_sat = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    iva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uso_cfdi = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    tipo_relacion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    xml_sellado = table.Column<string>(type: "text", nullable: true),
                    sello_digital = table.Column<string>(type: "text", nullable: true),
                    cadena_original = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    repuso_stock = table.Column<bool>(type: "boolean", nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notas_credito", x => x.id);
                    table.ForeignKey(
                        name: "FK_notas_credito_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notas_credito_facturas_factura_origen_id",
                        column: x => x.factura_origen_id,
                        principalTable: "facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notas_credito_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venta_cobros",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    factura_id = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    forma_pago_sat = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    fecha_pago_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    complemento_pago_id = table.Column<int>(type: "integer", nullable: true),
                    creado_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venta_cobros", x => x.id);
                    table.ForeignKey(
                        name: "FK_venta_cobros_complementos_pago_complemento_pago_id",
                        column: x => x.complemento_pago_id,
                        principalTable: "complementos_pago",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_venta_cobros_facturas_factura_id",
                        column: x => x.factura_id,
                        principalTable: "facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nota_credito_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nota_credito_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_nombre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nota_credito_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_nota_credito_detalle_notas_credito_nota_credito_id",
                        column: x => x.nota_credito_id,
                        principalTable: "notas_credito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nota_credito_detalle_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_fecha",
                table: "audit_logs",
                column: "fecha_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tipo",
                table: "audit_logs",
                column: "tipo");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_usuario",
                table: "audit_logs",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_cp",
                table: "clientes",
                column: "codigo_postal");

            migrationBuilder.CreateIndex(
                name: "ix_clientes_vendedor",
                table: "clientes",
                column: "vendedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_postales_asentamiento",
                table: "codigos_postales",
                column: "asentamiento");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_postales_cp",
                table: "codigos_postales",
                column: "codigo_postal");

            migrationBuilder.CreateIndex(
                name: "ix_codigos_postales_estado",
                table: "codigos_postales",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "uq_codigos_postales_cp_asentamiento",
                table: "codigos_postales",
                columns: new[] { "codigo_postal", "asentamiento_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comisiones_periodo",
                table: "comisiones_historial",
                columns: new[] { "anio", "mes", "estado" });

            migrationBuilder.CreateIndex(
                name: "uq_comisiones_historial_periodo",
                table: "comisiones_historial",
                columns: new[] { "vendedor_id", "anio", "mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_complemento_pago_detalle_complemento_pago_id",
                table: "complemento_pago_detalle",
                column: "complemento_pago_id");

            migrationBuilder.CreateIndex(
                name: "IX_complemento_pago_detalle_factura_id",
                table: "complemento_pago_detalle",
                column: "factura_id");

            migrationBuilder.CreateIndex(
                name: "IX_complementos_pago_cliente_id",
                table: "complementos_pago",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_complementos_pago_folio_interno",
                table: "complementos_pago",
                column: "folio_interno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_complementos_pago_sucursal_id",
                table: "complementos_pago",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_complementos_pago_uuid_sat",
                table: "complementos_pago",
                column: "uuid_sat",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compra_detalle_compra_id",
                table: "compra_detalle",
                column: "compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_detalle_producto_id",
                table: "compra_detalle",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_pagos_compra_id",
                table: "compra_pagos",
                column: "compra_id");

            migrationBuilder.CreateIndex(
                name: "IX_compras_proveedor_id",
                table: "compras",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_compras_sucursal_id",
                table: "compras",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "ix_cortes_sucursal_estado",
                table: "cortes_caja",
                columns: new[] { "sucursal_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_cuotas_periodo",
                table: "cuotas_vendedor",
                columns: new[] { "anio", "mes" });

            migrationBuilder.CreateIndex(
                name: "uq_cuotas_vendedor_periodo",
                table: "cuotas_vendedor",
                columns: new[] { "vendedor_id", "anio", "mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalle_asientos_asiento_contable_id",
                table: "detalle_asientos",
                column: "asiento_contable_id");

            migrationBuilder.CreateIndex(
                name: "IX_detalle_asientos_cuenta_id",
                table: "detalle_asientos",
                column: "cuenta_id");

            migrationBuilder.CreateIndex(
                name: "IX_factura_detalle_factura_id",
                table: "factura_detalle",
                column: "factura_id");

            migrationBuilder.CreateIndex(
                name: "IX_factura_detalle_producto_id",
                table: "factura_detalle",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_cliente_id",
                table: "facturas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_sucursal_id",
                table: "facturas",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_uuid_sat",
                table: "facturas",
                column: "uuid_sat",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_facturas_vendedor",
                table: "facturas",
                column: "vendedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventario_lotes_producto_id",
                table: "inventario_lotes",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventario_sucursal_lote_id",
                table: "inventario_sucursal",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventario_sucursal_producto_id",
                table: "inventario_sucursal",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "uq_sucursal_producto_lote",
                table: "inventario_sucursal",
                columns: new[] { "sucursal_id", "producto_id", "lote_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nota_credito_detalle_nota_credito_id",
                table: "nota_credito_detalle",
                column: "nota_credito_id");

            migrationBuilder.CreateIndex(
                name: "IX_nota_credito_detalle_producto_id",
                table: "nota_credito_detalle",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_notas_credito_cliente_id",
                table: "notas_credito",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_notas_credito_factura_origen_id",
                table: "notas_credito",
                column: "factura_origen_id");

            migrationBuilder.CreateIndex(
                name: "IX_notas_credito_folio_interno",
                table: "notas_credito",
                column: "folio_interno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notas_credito_sucursal_id",
                table: "notas_credito",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_notas_credito_uuid_sat",
                table: "notas_credito",
                column: "uuid_sat",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_histories_usuario",
                table: "password_histories",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_productos_categoria",
                table: "productos",
                column: "categoria");

            migrationBuilder.CreateIndex(
                name: "IX_productos_sku",
                table: "productos",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_productos_sku_anterior",
                table: "productos",
                column: "sku_anterior");

            migrationBuilder.CreateIndex(
                name: "ix_proveedores_cp",
                table: "proveedores",
                column: "codigo_postal");

            migrationBuilder.CreateIndex(
                name: "IX_ReglasAsiento_CuentaId",
                table: "ReglasAsiento",
                column: "CuentaId");

            migrationBuilder.CreateIndex(
                name: "IX_RolPermisos_PermisoId",
                table: "RolPermisos",
                column: "PermisoId");

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_codigo_postal",
                table: "sucursales",
                column: "codigo_postal");

            migrationBuilder.CreateIndex(
                name: "IX_sucursales_codigo_sucursal",
                table: "sucursales",
                column: "codigo_sucursal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidades_medida_producto_id",
                table: "unidades_medida",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_RoleId",
                table: "usuarios",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_sucursal_id",
                table: "usuarios",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_venta_cobros_complemento_pago_id",
                table: "venta_cobros",
                column: "complemento_pago_id");

            migrationBuilder.CreateIndex(
                name: "ix_venta_cobros_factura",
                table: "venta_cobros",
                column: "factura_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "codigos_postales");

            migrationBuilder.DropTable(
                name: "comisiones_historial");

            migrationBuilder.DropTable(
                name: "complemento_pago_detalle");

            migrationBuilder.DropTable(
                name: "compra_detalle");

            migrationBuilder.DropTable(
                name: "compra_pagos");

            migrationBuilder.DropTable(
                name: "configuracion");

            migrationBuilder.DropTable(
                name: "cortes_caja");

            migrationBuilder.DropTable(
                name: "cuotas_vendedor");

            migrationBuilder.DropTable(
                name: "detalle_asientos");

            migrationBuilder.DropTable(
                name: "factura_detalle");

            migrationBuilder.DropTable(
                name: "ImportacionParametros");

            migrationBuilder.DropTable(
                name: "ImportacionReglas");

            migrationBuilder.DropTable(
                name: "inventario_sucursal");

            migrationBuilder.DropTable(
                name: "movimientos_inventario");

            migrationBuilder.DropTable(
                name: "nota_credito_detalle");

            migrationBuilder.DropTable(
                name: "password_histories");

            migrationBuilder.DropTable(
                name: "ReglasAsiento");

            migrationBuilder.DropTable(
                name: "RolPermisos");

            migrationBuilder.DropTable(
                name: "unidades_medida");

            migrationBuilder.DropTable(
                name: "venta_cobros");

            migrationBuilder.DropTable(
                name: "compras");

            migrationBuilder.DropTable(
                name: "asientos_contables");

            migrationBuilder.DropTable(
                name: "inventario_lotes");

            migrationBuilder.DropTable(
                name: "notas_credito");

            migrationBuilder.DropTable(
                name: "cuentas_contables");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropTable(
                name: "complementos_pago");

            migrationBuilder.DropTable(
                name: "proveedores");

            migrationBuilder.DropTable(
                name: "productos");

            migrationBuilder.DropTable(
                name: "facturas");

            migrationBuilder.DropTable(
                name: "clientes");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "sucursales");
        }
    }
}

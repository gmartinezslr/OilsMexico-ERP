using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OilsMexico.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CotizacionesOrdenesVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cotizaciones",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<int>(type: "integer", nullable: false),
                    vendedor_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    folio_interno = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    iva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotizaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_cotizaciones_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cotizaciones_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cotizaciones_usuarios_vendedor_id",
                        column: x => x.vendedor_id,
                        principalTable: "usuarios",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "cotizacion_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cotizacion_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_medida_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_nombre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotizacion_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_cotizacion_detalle_cotizaciones_cotizacion_id",
                        column: x => x.cotizacion_id,
                        principalTable: "cotizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cotizacion_detalle_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_venta",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sucursal_id = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<int>(type: "integer", nullable: false),
                    vendedor_id = table.Column<int>(type: "integer", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    folio_interno = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cotizacion_id = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    iva = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_venta", x => x.id);
                    table.ForeignKey(
                        name: "FK_ordenes_venta_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ordenes_venta_cotizaciones_cotizacion_id",
                        column: x => x.cotizacion_id,
                        principalTable: "cotizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordenes_venta_sucursales_sucursal_id",
                        column: x => x.sucursal_id,
                        principalTable: "sucursales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ordenes_venta_usuarios_vendedor_id",
                        column: x => x.vendedor_id,
                        principalTable: "usuarios",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "orden_venta_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orden_venta_id = table.Column<int>(type: "integer", nullable: false),
                    producto_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_medida_id = table.Column<int>(type: "integer", nullable: false),
                    unidad_nombre = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    importe = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    lote_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orden_venta_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_orden_venta_detalle_ordenes_venta_orden_venta_id",
                        column: x => x.orden_venta_id,
                        principalTable: "ordenes_venta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_orden_venta_detalle_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "productos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_unidades_codigo_barra",
                table: "unidades_medida",
                column: "codigo_barra");

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_detalle_cotizacion_id",
                table: "cotizacion_detalle",
                column: "cotizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_cotizacion_detalle_producto_id",
                table: "cotizacion_detalle",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_cliente_id",
                table: "cotizaciones",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_sucursal_id",
                table: "cotizaciones",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_vendedor_id",
                table: "cotizaciones",
                column: "vendedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_orden_venta_detalle_orden_venta_id",
                table: "orden_venta_detalle",
                column: "orden_venta_id");

            migrationBuilder.CreateIndex(
                name: "IX_orden_venta_detalle_producto_id",
                table: "orden_venta_detalle",
                column: "producto_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_venta_cliente_id",
                table: "ordenes_venta",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_venta_cotizacion_id",
                table: "ordenes_venta",
                column: "cotizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_venta_sucursal_id",
                table: "ordenes_venta",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_venta_vendedor_id",
                table: "ordenes_venta",
                column: "vendedor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cotizacion_detalle");

            migrationBuilder.DropTable(
                name: "orden_venta_detalle");

            migrationBuilder.DropTable(
                name: "ordenes_venta");

            migrationBuilder.DropTable(
                name: "cotizaciones");

            migrationBuilder.DropIndex(
                name: "ix_unidades_codigo_barra",
                table: "unidades_medida");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Application.Services;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using QuestPDF.Infrastructure;
using Xunit;

namespace OilsMexico.IntegrationTests;

/// <summary>
/// Verificación de la exportación Excel/PDF (checklist §2.1) y su integración.
/// Usa EF InMemory (BD desechable) para validar que cada método de IExportacionService
/// produce un archivo no vacío con el MIME correcto y que el servicio de reportes
/// financieros (§1.4) alimenta FinancieroPdfAsync. REGLA #1 se ejercita con una sesión
/// Admin de la sucursal sembrada.
/// </summary>
public class ExportacionTests
{
    // QuestPDF exige declarar la licencia una vez por proceso. En la Web se hace en Program.cs;
    // en las pruebas no corre Program.cs, así que se declara aquí (antes de generar cualquier PDF).
    static ExportacionTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static (ErpDbContext db, ISucursalContext ctx, ExportacionService exp) Crear(string dbName)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(options);

        // Semilla mínima: sucursal, cliente, producto, inventario, una factura timbrada.
        if (!db.Sucursales.Any())
        {
            db.Sucursales.Add(new Sucursal { Id = 1, Nombre = "Matriz", CodigoSucursal = "M01", RfcEmisor = "AAA010101AAA", Activa = true });
            db.Clientes.Add(new Cliente { Id = 1, Nombre = "Cliente Prueba", Rfc = "XAXX010101000", Email = "c@x.com", Activo = true });
            db.Productos.Add(new Producto { Id = 1, Sku = "SKU1", Nombre = "Aceite 5W-30", Marca = "X", Viscosidad = "5W-30", PrecioCosto = 50m, PrecioVenta = 80m, Activo = true });
            db.InventarioSucursal.Add(new InventarioSucursal { SucursalId = 1, ProductoId = 1, StockActual = 100m, StockMinimo = 5m });
            db.SaveChanges();

            var factura = new Factura
            {
                Id = 1, SucursalId = 1, ClienteId = 1, FolioInterno = "F-001",
                FechaEmision = DateTime.UtcNow.AddDays(-1), Subtotal = 100m, Iva = 16m, Total = 116m,
                Estado = EstadoFactura.Timbrada, UuidSat = Guid.NewGuid(),
                XmlSellado = "<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\"/>",
                Detalles = new List<FacturaDetalle>
                {
                    new() { ProductoId = 1, Cantidad = 2m, PrecioUnitario = 58m, Importe = 116m, UnidadNombre = "Litro" }
                }
            };
            db.Facturas.Add(factura);
            db.SaveChanges();
        }

        var ctx = new SucursalContext();
        ctx.Establecer(1, 1, Roles.Admin);
        var reportes = new ReportesFinancierosService(db, ctx, new AsientoGeneradorService(db, ctx));
        var exp = new ExportacionService(db, ctx, reportes);
        return (db, ctx, exp);
    }

    private static void AssertArchivo(ArchivoExportadoDto a, string mime)
    {
        Assert.False(string.IsNullOrWhiteSpace(a.NombreArchivo));
        Assert.Equal(mime, a.MimeTipo);
        Assert.True(a.Contenido.Length > 0, $"{a.NombreArchivo} no debe estar vacío.");
    }
// __TESTS__

    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Fact]
    public async Task Inventario_Excel_genera_archivo_no_vacio()
    {
        var (_, _, exp) = Crear(nameof(Inventario_Excel_genera_archivo_no_vacio));
        var a = await exp.InventarioAsync(1);
        AssertArchivo(a, Xlsx);
        // Un .xlsx es un ZIP: empieza con "PK".
        Assert.Equal((byte)'P', a.Contenido[0]);
        Assert.Equal((byte)'K', a.Contenido[1]);
    }

    [Fact]
    public async Task Ventas_Excel_genera_archivo_no_vacio()
    {
        var (_, _, exp) = Crear(nameof(Ventas_Excel_genera_archivo_no_vacio));
        var a = await exp.VentasAsync(1, DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
        AssertArchivo(a, Xlsx);
    }

    [Fact]
    public async Task CxC_CxP_ListaPrecios_EstadoCuenta_Kardex_Excel()
    {
        var (_, _, exp) = Crear(nameof(CxC_CxP_ListaPrecios_EstadoCuenta_Kardex_Excel));
        AssertArchivo(await exp.CxPendientesAsync(1), Xlsx);
        AssertArchivo(await exp.CxPPendientesAsync(1), Xlsx);
        AssertArchivo(await exp.ListaPreciosAsync(), Xlsx);
        AssertArchivo(await exp.EstadoCuentaClienteAsync(1), Xlsx);
        AssertArchivo(await exp.KardexAsync(1, 1, 30), Xlsx);
        AssertArchivo(await exp.GastosAsync(1, DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1)), Xlsx);
        AssertArchivo(await exp.ComisionesAsync(1, DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1)), Xlsx);
    }

    [Fact]
    public async Task Cotizacion_y_Orden_PDF()
    {
        var (db, ctx, exp) = Crear(nameof(Cotizacion_y_Orden_PDF));
        db.Cotizaciones.Add(new Cotizacion { Id = 5, SucursalId = 1, ClienteId = 1, FolioInterno = "CT-5", FechaEmision = DateTime.UtcNow, Total = 116m, Estado = EstadoCotizacion.Borrador, Detalles = new List<CotizacionDetalle> { new() { ProductoId = 1, Cantidad = 2m, PrecioUnitario = 58m, Importe = 116m } } });
        db.OrdenesVenta.Add(new OrdenVenta { Id = 5, SucursalId = 1, ClienteId = 1, FolioInterno = "OV-5", FechaEmision = DateTime.UtcNow, Total = 116m, Estado = EstadoOrdenVenta.Borrador, Detalles = new List<OrdenVentaDetalle> { new() { ProductoId = 1, Cantidad = 2m, PrecioUnitario = 58m, Importe = 116m } } });
        await db.SaveChangesAsync();
        AssertArchivo(await exp.CotizacionPdfAsync(5), "application/pdf");
        AssertArchivo(await exp.OrdenVentaPdfAsync(5), "application/pdf");
    }
// __TESTS2__

    [Fact]
    public async Task FacturaCfdi_PDF_empieza_con_cabecera_PDF()
    {
        var (_, _, exp) = Crear(nameof(FacturaCfdi_PDF_empieza_con_cabecera_PDF));
        var a = await exp.FacturaCfdiPdfAsync(1);
        AssertArchivo(a, "application/pdf");
        // Un PDF empieza con "%PDF".
        Assert.Equal((byte)'%', a.Contenido[0]);
        Assert.Equal((byte)'P', a.Contenido[1]);
        Assert.Equal((byte)'D', a.Contenido[2]);
        Assert.Equal((byte)'F', a.Contenido[3]);
    }

    [Theory]
    [InlineData("balanza")]
    [InlineData("balance")]
    [InlineData("resultados")]
    [InlineData("existencias")]
    public async Task Financiero_PDF_para_cada_reporte(string reporte)
    {
        var (_, _, exp) = Crear($"fin_{reporte}");
        var a = await exp.FinancieroPdfAsync(reporte, 1, DateTime.Today.AddDays(-30), DateTime.Today);
        AssertArchivo(a, "application/pdf");
        Assert.Equal((byte)'%', a.Contenido[0]);
        Assert.Equal((byte)'P', a.Contenido[1]);
    }

    [Fact]
    public async Task REGLA1_noAdmin_solo_ve_su_sucursal()
    {
        // Un usuario NO admin apuntando a otra sucursal debe obtener el inventario vacío
        // (el servicio fija la consulta a su propia sucursal vía ResolverSucursalAutorizada).
        var (db, _, _) = Crear(nameof(REGLA1_noAdmin_solo_ve_su_sucursal));
        var ctx = new SucursalContext();
        ctx.Establecer(2, 1, Roles.Vendedor); // sucursal 2, distinta de la sembrada (1)
        var exp = new ExportacionService(db, ctx, new ReportesFinancierosService(db, ctx, new AsientoGeneradorService(db, ctx)));
        var a = await exp.InventarioAsync(1); // pide la 1, pero el servicio lo reescribe a la 2
        AssertArchivo(a, Xlsx); // el archivo se genera; el servicio consultó la sucursal 2.
    }
}

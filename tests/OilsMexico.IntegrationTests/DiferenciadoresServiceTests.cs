using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Services;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using Xunit;

namespace OilsMexico.IntegrationTests;

public sealed class DiferenciadoresServiceTests
{
    [Fact]
    public async Task Precio_escalonado_resuelve_litros_y_conserva_precio_base_fuera_del_umbral()
    {
        await using var db = NuevaDb();
        var cliente = new Cliente { Id = 1, Nombre = "Mayorista", TipoPrecio = "mayoreo" };
        var producto = new Producto { Id = 1, Sku = "ACE-1", Nombre = "Aceite", PrecioVenta = 120, PrecioMayoreo = 100 };
        var lista = new ListaPrecio
        {
            Id = 1, Nombre = "Mayoreo base", TipoCliente = "mayoreo", Activa = true,
            Precios = [new PrecioVolumen { ProductoId = 1, CantidadMinima = 20, PrecioUnitario = 85 }]
        };
        db.AddRange(cliente, producto, lista);
        await db.SaveChangesAsync();
        var servicio = Servicio(db);

        var sinUmbral = await servicio.ResolverPrecioAsync(1, 1, 5, 2);
        var conUmbral = await servicio.ResolverPrecioAsync(1, 1, 10, 2);
        var batch = await servicio.ResolverPreciosAsync([1], 1, new Dictionary<int, decimal> { [1] = 20m });

        Assert.Equal(100m, sinUmbral.Precio);
        Assert.Equal(85m, conUmbral.Precio);
        Assert.Equal(85m, batch[1].Precio);
        Assert.Contains("Mayoreo base", conUmbral.Origen);
    }

    [Fact]
    public async Task Listas_de_precio_solo_puede_modificarlas_el_administrador()
    {
        await using var db = NuevaDb();
        var contexto = new SucursalContext();
        contexto.Establecer(1, 7, Roles.Vendedor);
        var servicio = new DiferenciadoresService(db, contexto);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            servicio.GuardarListaPrecioAsync(0, "Mayoreo", "mayoreo", true));
    }

    [Fact]
    public async Task Compatibilidad_se_consulta_por_marca_modelo_y_rango_de_anios_aun_sin_producto_asociado()
    {
        await using var db = NuevaDb();
        db.CompatibilidadesVehiculo.Add(new CompatibilidadVehiculo
        {
            Marca = "Toyota", Modelo = "Corolla", AnioDesde = 2018, AnioHasta = 2022,
            Motor = "2.0 L", Viscosidad = "0W-20", Norma = "API SP", CapacidadLitros = 4.2m
        });
        await db.SaveChangesAsync();

        var resultado = await Servicio(db).BuscarCompatibilidadAsync("toy", "cor", 2020);

        var aplicacion = Assert.Single(resultado);
        Assert.Equal("0W-20", aplicacion.Viscosidad);
        Assert.Equal(4.2m, aplicacion.CapacidadLitros);
        Assert.Null(aplicacion.Producto);
    }

    [Fact]
    public async Task Evaluacion_de_proveedor_solo_se_guarda_para_compra_recibida_y_calcula_promedio()
    {
        await using var db = NuevaDb();
        db.Proveedores.Add(new Proveedor { Id = 1, Nombre = "Proveedor", Rfc = "AAA010101AA0" });
        db.Compras.Add(new Compra { Id = 1, SucursalId = 1, ProveedorId = 1, FolioInterno = "C-1", Estado = EstadoCompra.Recibida });
        await db.SaveChangesAsync();
        var servicio = Servicio(db);

        await servicio.EvaluarProveedorAsync(new EvaluacionProveedorRequest(1, 4, 2, "Llegó incompleto"));
        var calificacion = Assert.Single(await servicio.ListarEvaluacionesProveedorAsync());

        Assert.Equal(3m, calificacion.Promedio);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EvaluarProveedorAsync(new EvaluacionProveedorRequest(1, 5, 5, null)));
    }

    [Fact]
    public async Task Liquidacion_exige_disposicion_completa_y_no_resta_inventario_dos_veces()
    {
        await using var db = NuevaDb();
        var (orden, producto) = CrearOrdenFacturada();
        db.AddRange(orden, producto, new Cliente { Id = 1, Nombre = "Cliente" },
            new Sucursal { Id = 1, Nombre = "Principal", CodigoSucursal = "01", Direccion = "Centro", RfcEmisor = "AAA010101AA0" },
            new Factura { Id = 1, SucursalId = 1, ClienteId = 1, FolioInterno = "V-1", Estado = EstadoFactura.Timbrada },
            new InventarioSucursal { Id = 1, SucursalId = 1, ProductoId = 1, StockActual = 25 });
        await db.SaveChangesAsync();
        var servicio = Servicio(db);
        var entregaId = await servicio.ProgramarEntregaAsync(new EntregaCrearRequest(
            orden.Id, "Centro", "Chofer", "Camioneta", "ABC123", DateTime.UtcNow.AddHours(1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.LiquidarEntregaAsync(
            new EntregaLiquidarRequest(entregaId, [new EntregaLiquidarRenglonDto(1, 7, 2)])));
        await servicio.LiquidarEntregaAsync(new EntregaLiquidarRequest(
            entregaId, [new EntregaLiquidarRenglonDto(1, 8, 2)]));

        Assert.Equal(25m, (await db.InventarioSucursal.SingleAsync()).StockActual);
        var entrega = Assert.Single(await servicio.ListarEntregasAsync());
        Assert.Equal("Liquidada", entrega.Estado);
        Assert.Equal(2m, entrega.Detalles[0].Devuelta);
    }

    [Fact]
    public async Task Retornables_conservan_saldo_y_deposito_promedio_al_recibir_devolucion()
    {
        await using var db = NuevaDb();
        db.Clientes.Add(new Cliente { Id = 1, Nombre = "Taller" });
        await db.SaveChangesAsync();
        var servicio = Servicio(db);

        await servicio.RegistrarMovimientoEnvaseAsync(new EnvaseMovimientoRequest(1, "Cubeta 19 L", 2, 300, false, "V-1"));
        await servicio.RegistrarMovimientoEnvaseAsync(new EnvaseMovimientoRequest(1, "Cubeta 19 L", 1, 0, true, "DEV-1"));
        var saldo = Assert.Single(await servicio.ListarSaldosEnvasesAsync());

        Assert.Equal(1, saldo.Saldo);
        Assert.Equal(300m, saldo.DepositoPendiente);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.RegistrarMovimientoEnvaseAsync(new EnvaseMovimientoRequest(1, "Cubeta 19 L", 2, 0, true, null)));
    }

    [Fact]
    public async Task Control_tambor_no_puede_clasificar_mas_litros_que_el_stock_disponible()
    {
        await using var db = NuevaDb();
        db.AddRange(
            new Producto { Id = 1, Sku = "ACE-1", Nombre = "Aceite" },
            new InventarioSucursal { Id = 1, SucursalId = 1, ProductoId = 1, StockActual = 300 });
        await db.SaveChangesAsync();
        var servicio = Servicio(db);

        await servicio.GuardarControlTamborAsync(0, 1, 208, 1, 92);

        var control = Assert.Single(await servicio.ListarControlesTamborAsync());
        Assert.Equal(1, control.TamboresCerrados);
        Assert.Equal(92m, control.LitrosAbiertos);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.GuardarControlTamborAsync(0, 1, 208, 2, 0));
    }

    [Fact]
    public async Task Carta_porte_guarda_xml_borrador_con_version_31_y_sin_marcarlo_timbrado()
    {
        await using var db = NuevaDb();
        var (orden, producto) = CrearOrdenFacturada();
        db.AddRange(orden, producto, new Cliente { Id = 1, Nombre = "Cliente" },
            new Sucursal { Id = 1, Nombre = "Principal", CodigoSucursal = "01", Direccion = "Centro", RfcEmisor = "AAA010101AA0" },
            new Factura { Id = 1, SucursalId = 1, ClienteId = 1, FolioInterno = "V-1", Estado = EstadoFactura.Timbrada });
        await db.SaveChangesAsync();
        var servicio = Servicio(db);
        var entregaId = await servicio.ProgramarEntregaAsync(new EntregaCrearRequest(
            orden.Id, "Centro", "Chofer", "Camioneta", "ABC123", DateTime.UtcNow.AddHours(1)));

        var documento = await servicio.GuardarCartaPorteAsync(new CartaPorteRequest(
            entregaId, "AAA010101AA0", "TPAF01", "PERM-001", "C2", "ABC123", 900,
            "06600", "06700"));
        var xml = XDocument.Parse(documento.XmlBorrador);

        Assert.Equal("3.1", xml.Descendants().Single(x => x.Name.LocalName == "CartaPorte").Attribute("Version")?.Value);
        Assert.Contains("CartaPorte31", documento.XmlBorrador);
        Assert.True((await servicio.ListarEntregasAsync()).Single().TieneCartaPorte);
    }

    private static (OrdenVenta Orden, Producto Producto) CrearOrdenFacturada()
    {
        var producto = new Producto { Id = 1, Sku = "ACE-1", Nombre = "Aceite", Marca = "Marca", Viscosidad = "5W-30", TipoBase = "Sintético" };
        var orden = new OrdenVenta
        {
            Id = 1, SucursalId = 1, ClienteId = 1, UsuarioId = 7, FolioInterno = "OV-1", FacturaId = 1,
            Estado = EstadoOrdenVenta.Facturada,
            Detalles = [new OrdenVentaDetalle
            {
                ProductoId = producto.Id, Producto = producto, UnidadMedidaId = 1,
                UnidadNombre = "Tambor", Cantidad = 10, PrecioUnitario = 100, Importe = 1000
            }]
        };
        return (orden, producto);
    }

    private static DiferenciadoresService Servicio(ErpDbContext db)
    {
        var contexto = new SucursalContext();
        contexto.Establecer(1, 7, Roles.Admin);
        return new DiferenciadoresService(db, contexto);
    }

    private static ErpDbContext NuevaDb() => new(new DbContextOptionsBuilder<ErpDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

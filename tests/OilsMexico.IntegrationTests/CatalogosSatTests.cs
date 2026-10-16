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

public sealed class CatalogosSatTests
{
    private static async Task<ErpDbContext> CrearDbAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(nombre)
            .Options;
        var db = new ErpDbContext(options);
        await CatalogosSatSeed.AsegurarAsync(db);
        return db;
    }

    [Fact]
    public async Task CatalogosSat_CargaDesdeBaseDeDatos_Correctamente()
    {
        await using var db = await CrearDbAsync(nameof(CatalogosSat_CargaDesdeBaseDeDatos_Correctamente));
        var servicio = new CatalogosSatService(db);

        var dto = await servicio.ObtenerAsync();

        Assert.NotNull(dto);
        Assert.True(dto.FormasPago.ContainsKey("01"));
        Assert.Equal("Efectivo", dto.FormasPago["01"]);
        Assert.True(dto.FormasPago.ContainsKey("03"));
        Assert.True(dto.MetodosPago.ContainsKey("PUE"));
        Assert.True(dto.MetodosPago.ContainsKey("PPD"));
        Assert.True(dto.UsosCfdi.ContainsKey("G03"));
        Assert.True(dto.Regimenes.ContainsKey("601"));
        Assert.True(dto.MotivosCancelacion.ContainsKey("01"));
        Assert.True(dto.SituacionesFiscales.ContainsKey("Actual"));
        Assert.True(dto.GruposFormasPago.ContainsKey("01"));
        Assert.Equal("efectivo", dto.GruposFormasPago["01"]);
        Assert.Equal("tarjeta", dto.GruposFormasPago["04"]);
        Assert.Equal("tarjeta", dto.GruposFormasPago["28"]);
        Assert.Equal("transferencia", dto.GruposFormasPago["03"]);
    }

    [Fact]
    public void CatalogosSat_FallbackSinDb_ProveeValoresPorDefecto()
    {
        var servicio = new CatalogosSatService();

        var dto = servicio.Obtener();

        Assert.NotNull(dto);
        Assert.True(dto.FormasPago.Count > 0);
        Assert.True(dto.MetodosPago.Count > 0);
        Assert.True(dto.UsosCfdi.Count > 0);
        Assert.True(dto.Regimenes.Count > 0);
        Assert.True(dto.MotivosCancelacion.Count > 0);
        Assert.True(dto.SituacionesFiscales.Count > 0);
        Assert.Equal("efectivo", servicio.ObtenerGrupoFormaPago("01"));
        Assert.Equal("tarjeta", servicio.ObtenerGrupoFormaPago("04"));
        Assert.Equal("tarjeta", servicio.ObtenerGrupoFormaPago("28"));
        Assert.Equal("transferencia", servicio.ObtenerGrupoFormaPago("03"));
    }

    [Fact]
    public async Task CatalogosSat_PermiteNuevasFormasDePago_SinModificarCodigo_OCP()
    {
        await using var db = await CrearDbAsync(nameof(CatalogosSat_PermiteNuevasFormasDePago_SinModificarCodigo_OCP));
        db.CatalogosSat.Add(new CatalogoSat
        {
            Catalogo = "c_FormaPago",
            Clave = "05",
            Descripcion = "Monedero electrónico",
            Grupo = "tarjeta",
            Activo = true,
            Orden = 7
        });
        await db.SaveChangesAsync();

        var servicio = new CatalogosSatService(db);
        var dto = await servicio.ObtenerAsync();

        Assert.True(dto.FormasPago.ContainsKey("05"));
        Assert.Equal("Monedero electrónico", dto.FormasPago["05"]);
        Assert.Equal("tarjeta", servicio.ObtenerGrupoFormaPago("05"));
    }

    [Fact]
    public async Task CorteCaja_ClasificaFormasDePago_UsandoGrupoDinamico()
    {
        await using var db = await CrearDbAsync(nameof(CorteCaja_ClasificaFormasDePago_UsandoGrupoDinamico));
        var ctx = new SucursalContext();
        ctx.Establecer(1, 1, Roles.Admin);

        var sat = new CatalogosSatService(db);
        var corteSvc = new CorteCajaService(db, ctx, sat);

        // Crear sucursal y corte abierto
        var sucursal = new Sucursal { Id = 1, Nombre = "Sucursal Test", CodigoSucursal = "S01", Direccion = "Calle 1" };
        var corte = new CorteCaja
        {
            Id = 1,
            SucursalId = 1,
            UsuarioAperturaId = 1,
            UsuarioAperturaNombre = "Admin",
            FechaAperturaUtc = DateTime.UtcNow.AddHours(-2),
            FondoInicial = 500m,
            Estado = EstadoCorte.Abierto
        };
        db.Sucursales.Add(sucursal);
        db.CortesCaja.Add(corte);

        // Ventas en distintas formas de pago
        db.Facturas.AddRange(
            new Factura { Id = 1, SucursalId = 1, FechaEmision = DateTime.UtcNow.AddMinutes(-30), Total = 100m, FormaPagoSat = "01", Estado = EstadoFactura.Timbrada }, // Efectivo
            new Factura { Id = 2, SucursalId = 1, FechaEmision = DateTime.UtcNow.AddMinutes(-20), Total = 200m, FormaPagoSat = "04", Estado = EstadoFactura.Timbrada }, // Tarjeta crédito
            new Factura { Id = 3, SucursalId = 1, FechaEmision = DateTime.UtcNow.AddMinutes(-15), Total = 150m, FormaPagoSat = "28", Estado = EstadoFactura.Timbrada }, // Tarjeta débito
            new Factura { Id = 4, SucursalId = 1, FechaEmision = DateTime.UtcNow.AddMinutes(-10), Total = 300m, FormaPagoSat = "03", Estado = EstadoFactura.Timbrada }, // Transferencia
            new Factura { Id = 5, SucursalId = 1, FechaEmision = DateTime.UtcNow.AddMinutes(-5), Total = 50m, FormaPagoSat = "02", Estado = EstadoFactura.Timbrada }   // Cheque (otro)
        );
        await db.SaveChangesAsync();

        var preview = await corteSvc.PreviewAsync(1);

        Assert.Equal(500m, preview.FondoInicial);
        Assert.Equal(100m, preview.EfectivoSistema);
        Assert.Equal(350m, preview.TarjetaSistema); // 200 (04) + 150 (28)
        Assert.Equal(300m, preview.TransferSistema);
        Assert.Equal(50m, preview.OtrosSistema);
        Assert.Equal(800m, preview.TotalVentasSistema);
    }
}

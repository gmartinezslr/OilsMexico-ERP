using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using Xunit;

namespace OilsMexico.IntegrationTests;

public sealed class TerceroContactosServiceTests
{
    [Fact]
    public async Task GuardarCliente_persiste_contactos_medios_y_direcciones_multiples()
    {
        await using var db = NuevaDb();
        var principal = new ContactoTerceroDto
        {
            Clave = "principal",
            Nombre = "Alex Ejemplo",
            EsPrincipal = true
        };
        var respaldo = new ContactoTerceroDto
        {
            Clave = "respaldo",
            Nombre = "Sam Ejemplo",
            EsRespaldo = true,
            PuedeRecibirPedidos = true
        };
        var servicio = new ClientesService(db);

        var resultado = await servicio.GuardarAsync(new ClienteCatalogoDto
        {
            Nombre = "Cliente Ejemplo",
            Rfc = "AAA010101AA0",
            Contactos = [principal, respaldo],
            Correos =
            [
                new() { Direccion = "principal@example.com", ContactoClave = principal.Clave, EsPrincipal = true },
                new() { Tipo = "Facturación", Direccion = "facturas@example.com" }
            ],
            Telefonos =
            [
                new() { Numero = "+525500000001", ContactoClave = respaldo.Clave, TieneWhatsApp = true }
            ],
            Direcciones =
            [
                new() { Tipo = "Fiscal", EsPredeterminada = true, CodigoPostal = "06600", Colonia = "Centro" },
                new() { Tipo = "Entrega", EsPredeterminada = true, CodigoPostal = "06700", Colonia = "Roma" }
            ]
        });

        Assert.True(resultado.Ok, resultado.Mensaje);
        var cliente = await servicio.ObtenerAsync(resultado.Id);
        Assert.NotNull(cliente);
        Assert.Equal(2, cliente.Contactos.Count);
        Assert.Equal(2, cliente.Correos.Count);
        Assert.Single(cliente.Telefonos);
        Assert.Equal(2, cliente.Direcciones.Count);
        Assert.Equal(cliente.Contactos.Single(x => x.EsRespaldo).Clave, cliente.Telefonos[0].ContactoClave);
    }

    [Fact]
    public async Task GuardarCliente_rechaza_respaldo_sin_autorizacion_de_recepcion()
    {
        await using var db = NuevaDb();
        var resultado = await new ClientesService(db).GuardarAsync(new ClienteCatalogoDto
        {
            Nombre = "Cliente Ejemplo",
            Rfc = "AAA010101AA0",
            Contactos =
            [
                new() { Nombre = "Alex Ejemplo", EsPrincipal = true },
                new() { Clave = "respaldo", Nombre = "Sam Ejemplo", EsRespaldo = true }
            ],
            Telefonos = [new() { Numero = "+525500000001", ContactoClave = "respaldo" }],
            Direcciones = [new() { Tipo = "Fiscal", EsPredeterminada = true, CodigoPostal = "06600", Colonia = "Centro" }]
        });

        Assert.False(resultado.Ok);
        Assert.Contains("recibir pedidos o notificaciones", resultado.Mensaje);
        Assert.Equal(0, resultado.Id);
    }

    [Fact]
    public async Task GuardarProveedor_rechaza_contacto_sin_puesto()
    {
        await using var db = NuevaDb();
        var resultado = await new ProveedoresService(db).GuardarAsync(new ProveedorCatalogoDto
        {
            Nombre = "Proveedor Ejemplo",
            Rfc = "BBB010101BB1",
            Contactos = [new() { Nombre = "Robin Ejemplo", EsPrincipal = true }]
        });

        Assert.False(resultado.Ok);
        Assert.Contains("puesto o cargo", resultado.Mensaje);
        Assert.Equal(0, resultado.Id);
    }

    private static ErpDbContext NuevaDb()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ErpDbContext(options);
    }
}

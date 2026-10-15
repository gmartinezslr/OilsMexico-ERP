using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using Xunit;

namespace OilsMexico.IntegrationTests;

[Collection(PostgresP1Collection.Name)]
public sealed class TerceroContactosPatchTests(PgFixture fixture)
{
    [Fact]
    public async Task SchemaPatch_migra_contacto_plano_a_medios_y_direcciones_idempotentemente()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        {
            var cliente = new Cliente
            {
                Nombre = "Cliente legacy handoff",
                Rfc = "AAA010101AA0",
                Email = "cliente@example.test",
                Telefono = "+525500000001",
                Calle = "Calle de ejemplo",
                NumeroExterior = "10",
                Colonia = "Centro",
                CodigoPostal = "06600",
                Municipio = "Ciudad de México",
                Estado = "Ciudad de México",
                Ciudad = "Ciudad de México",
                Pais = "México",
                Direccion = "Calle de ejemplo 10, Centro"
            };
            var proveedor = new Proveedor
            {
                Nombre = "Proveedor legacy handoff",
                Rfc = "BBB010101BB1",
                Email = "proveedor@example.test",
                Telefono = "+525500000002",
                Calle = "Av. Muestra",
                NumeroExterior = "20",
                Colonia = "Industrial",
                CodigoPostal = "06700",
                Municipio = "Ciudad de México",
                Estado = "Ciudad de México",
                Ciudad = "Ciudad de México",
                Pais = "México",
                Direccion = "Av. Muestra 20, Industrial"
            };
            db.AddRange(cliente, proveedor);
            await db.SaveChangesAsync();

            await TerceroContactosPatch.AplicarAsync(db);
            await TerceroContactosPatch.AplicarAsync(db);

            Assert.Equal("cliente@example.test",
                await db.CorreosTercero.Where(x => x.ClienteId == cliente.Id).Select(x => x.Direccion).SingleAsync());
            Assert.Equal("+525500000001",
                await db.TelefonosTercero.Where(x => x.ClienteId == cliente.Id).Select(x => x.Numero).SingleAsync());
            Assert.Equal("Calle de ejemplo",
                await db.DireccionesTercero.Where(x => x.ClienteId == cliente.Id).Select(x => x.Calle).SingleAsync());
            Assert.Equal("proveedor@example.test",
                await db.CorreosTercero.Where(x => x.ProveedorId == proveedor.Id).Select(x => x.Direccion).SingleAsync());
            Assert.Equal("+525500000002",
                await db.TelefonosTercero.Where(x => x.ProveedorId == proveedor.Id).Select(x => x.Numero).SingleAsync());
            Assert.Equal("Av. Muestra",
                await db.DireccionesTercero.Where(x => x.ProveedorId == proveedor.Id).Select(x => x.Calle).SingleAsync());
        }
    }

    [Fact]
    public async Task Servicio_actualiza_el_conjunto_de_contactos_sin_dejar_medios_huerfanos()
    {
        var (db, _) = await fixture.NuevaDbAsync();
        await using (db)
        {
            var principal = new ContactoTerceroDto { Clave = "principal", Nombre = "Alex Ejemplo", EsPrincipal = true };
            var respaldo = new ContactoTerceroDto
            {
                Clave = "respaldo",
                Nombre = "Sam Ejemplo",
                EsRespaldo = true,
                PuedeRecibirNotificaciones = true
            };
            var servicio = new ClientesService(db);
            var guardado = await servicio.GuardarAsync(new ClienteCatalogoDto
            {
                Nombre = $"Cliente update {Guid.NewGuid():N}",
                Rfc = "AAA010101AA0",
                Contactos = [principal, respaldo],
                Correos = [new() { Direccion = "alex@example.test", ContactoClave = principal.Clave, EsPrincipal = true }],
                Telefonos = [new() { Numero = "+525500000001", ContactoClave = respaldo.Clave }],
                Direcciones = [new() { Tipo = "Fiscal", EsPredeterminada = true, CodigoPostal = "06600", Colonia = "Centro" }]
            });
            Assert.True(guardado.Ok, guardado.Mensaje);

            var editado = await servicio.ObtenerAsync(guardado.Id);
            Assert.NotNull(editado);
            editado.Correos[0].Direccion = "actualizado@example.test";
            editado.Telefonos[0].Numero = "+525500000009";
            var resultado = await servicio.GuardarAsync(editado);

            Assert.True(resultado.Ok, resultado.Mensaje);
            var verificado = await servicio.ObtenerAsync(guardado.Id);
            Assert.NotNull(verificado);
            Assert.Equal(2, verificado.Contactos.Count);
            Assert.Equal("actualizado@example.test", Assert.Single(verificado.Correos).Direccion);
            Assert.Equal("+525500000009", Assert.Single(verificado.Telefonos).Numero);
            Assert.Equal(verificado.Contactos.Single(x => x.EsRespaldo).Clave, verificado.Telefonos[0].ContactoClave);
        }
    }
}

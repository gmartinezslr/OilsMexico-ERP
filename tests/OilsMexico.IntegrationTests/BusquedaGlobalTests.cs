using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using Xunit;

namespace OilsMexico.IntegrationTests;

public sealed class BusquedaGlobalTests
{
    [Fact]
    public async Task BuscarAsync_IncluyeSaldoVencidoYNivelDeCreditoDelCliente()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ErpDbContext(options);
        var cliente = new Cliente
        {
            Nombre = "Cliente Crédito",
            LimiteCredito = 50m,
            DiasCredito = 30
        };
        var vencida = new Factura
        {
            SucursalId = 1,
            Cliente = cliente,
            Total = 100m,
            FechaEmision = DateTime.UtcNow.AddDays(-35),
            Estado = EstadoFactura.Pendiente
        };
        var cancelada = new Factura
        {
            SucursalId = 1,
            Cliente = cliente,
            Total = 500m,
            FechaEmision = DateTime.UtcNow.AddDays(-35),
            Estado = EstadoFactura.Cancelada
        };
        db.AddRange(cliente, vencida, cancelada);
        await db.SaveChangesAsync();
        db.VentaCobros.Add(new VentaCobro
        {
            SucursalId = 1,
            FacturaId = vencida.Id,
            ClienteId = cliente.Id,
            Monto = 30m,
            UsuarioId = 1
        });
        await db.SaveChangesAsync();

        var resultados = await new BusquedaGlobalService(db).BuscarAsync("Cliente Crédito", 1);

        var credito = Assert.Single(resultados).Credito;
        Assert.NotNull(credito);
        Assert.Equal(70m, credito.SaldoVencido);
        Assert.Equal(50m, credito.LimiteCredito);
        Assert.True(credito.EstaBloqueado);
    }
}

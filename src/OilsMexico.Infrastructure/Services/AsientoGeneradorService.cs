using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class AsientoGeneradorService : IAsientoGeneradorService
{
    public Task<IReadOnlyList<(int tipo, decimal importe)>> GenerarAsientosDeVentaAsync(
        int sucursalId, int usuarioId, decimal total, decimal subtotal, decimal iva, string metodoPagoSat, string formaPagoSat)
    {
        var list = new List<(int tipo, decimal importe)>();

        if (metodoPagoSat == "PPD")
        {
            list.Add((1, total));   // 1100 Cuentas por cobrar
            list.Add((2, subtotal)); // 3000 Ventas
            if (iva > 0)
                list.Add((2, iva));  // 4000 IVA a cobrar
        }
        else
        {
            list.Add((1, total));    // 1000 Efectivo
            list.Add((2, subtotal)); // 3000 Ventas
            if (iva > 0)
                list.Add((2, iva));  // 4000 IVA a cobrar
        }

        return Task.FromResult<IReadOnlyList<(int tipo, decimal importe)>>(list.AsReadOnly());
    }

    public Task<IReadOnlyList<(int tipo, decimal importe)>> GenerarAsientosDeCompraAsync(int sucursalId, int usuarioId, decimal total)
    {
        return Task.FromResult<IReadOnlyList<(int tipo, decimal importe)>>(new[]
        {
            (1, total) // 1200 Mercancía / Compras
        }.AsReadOnly());
    }

    public Task<IReadOnlyList<(int tipo, decimal importe)>> GenerarAsientosDeDevolucionAsync(int sucursalId, int usuarioId, decimal total)
    {
        return Task.FromResult<IReadOnlyList<(int tipo, decimal importe)>>(new[]
        {
            (2, total) // 3100 Devoluciones y descuentos
        }.AsReadOnly());
    }
}

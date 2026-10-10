using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class AsientoGeneradorService(ErpDbContext db) : IAsientoGeneradorService
{
    public async Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeVentaAsync(
        int sucursalId, int usuarioId, decimal total, decimal subtotal, decimal iva, string metodoPagoSat, string formaPagoSat)
    {
        var reglas = await db.ReglasAsiento
            .Where(r => r.Evento == "Venta" && r.Activo)
            .OrderBy(r => r.Orden)
            .ToListAsync();

        var lineas = new List<AsientoLinea>();
        foreach (var r in reglas)
        {
            decimal importe = r.MontoOrigen switch
            {
                "total" => total,
                "subtotal" => subtotal,
                "neto" => subtotal,
                "iva" => iva,
                _ => 0m
            };

            // No emitir IVA si es cero (regla de corregir el vigente).
            if (r.MontoOrigen == "iva" && importe <= 0) continue;

            var cuenta = await db.CuentasContables.FindAsync([r.CuentaId]);
            if (cuenta is null) continue;

            lineas.Add(new AsientoLinea
            {
                CuentaCodigo = cuenta.Codigo,
                Debe = r.Debe,
                Importe = importe
            });
        }

        return lineas.AsReadOnly();
    }

    public Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeCompraAsync(int sucursalId, int usuarioId, decimal total)
    {
        return GenerarConEventoAsync("Compra", total, 0m, 0m);
    }

    public Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeDevolucionAsync(int sucursalId, int usuarioId, decimal total)
    {
        return GenerarConEventoAsync("Devolucion", total, 0m, 0m);
    }

    private async Task<IReadOnlyList<AsientoLinea>> GenerarConEventoAsync(string evento, decimal montoTotal, decimal montoSubtotal, decimal montoIva)
    {
        var reglas = await db.ReglasAsiento
            .Where(r => r.Evento == evento && r.Activo)
            .OrderBy(r => r.Orden)
            .ToListAsync();

        var lineas = new List<AsientoLinea>();
        foreach (var r in reglas)
        {
            decimal importe = r.MontoOrigen switch
            {
                "total" => montoTotal,
                "subtotal" => montoSubtotal,
                "iva" => montoIva,
                "neto" => montoSubtotal,
                _ => 0m
            };

            if (r.MontoOrigen == "iva" && importe <= 0) continue;

            var cuenta = await db.CuentasContables.FindAsync([r.CuentaId]);
            if (cuenta is null) continue;

            lineas.Add(new AsientoLinea
            {
                CuentaCodigo = cuenta.Codigo,
                Debe = r.Debe,
                Importe = importe
            });
        }

        return lineas.AsReadOnly();
    }
}

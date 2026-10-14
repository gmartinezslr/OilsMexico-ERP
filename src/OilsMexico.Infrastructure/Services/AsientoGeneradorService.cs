using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class AsientoGeneradorService(ErpDbContext db, ISucursalContext ctx) : IAsientoGeneradorService
{
    public async Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeVentaAsync(
        int sucursalId, int usuarioId, decimal total, decimal subtotal, decimal iva, string metodoPagoSat, string formaPagoSat)
    {
        var reglas = await db.ReglasAsiento
            .Where(r => r.Evento == "Venta" && r.Activo)
            .OrderBy(r => r.Orden)
            .ToListAsync();

        // Carga por lote de las cuentas referenciadas (§6.2: elimina la consulta N+1 por regla).
        var cuentasPorId = await CargarCuentasAsync(reglas.Select(r => r.CuentaId), sucursalId);

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

            if (!cuentasPorId.TryGetValue(r.CuentaId, out var cuenta)) continue;

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
        return GenerarConEventoAsync("Compra", sucursalId, total, 0m, 0m);
    }

    public Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeDevolucionAsync(int sucursalId, int usuarioId, decimal total)
    {
        return GenerarConEventoAsync("Devolucion", sucursalId, total, 0m, 0m);
    }

    public Task<IReadOnlyList<AsientoLinea>> GenerarAsientosDeGastoAsync(int sucursalId, int usuarioId, decimal total)
    {
        return GenerarConEventoAsync("Gasto", sucursalId, total, 0m, 0m);
    }

    /// <summary>
    /// Carga las cuentas de los ids dados de UNA sola consulta. Si <paramref name="sucursalId"/>
    /// es positivo se restringe a esa sucursal (REGLA #1): una regla que apunte a una cuenta de
    /// otra sucursal no genera línea, en lugar de usarla a ciegas.
    /// </summary>
    private async Task<Dictionary<int, CuentaContable>> CargarCuentasAsync(IEnumerable<int> cuentaIds, int sucursalId)
    {
        var ids = cuentaIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, CuentaContable>();
        var q = db.CuentasContables.AsNoTracking().Where(c => ids.Contains(c.Id));
        if (sucursalId > 0) q = q.Where(c => c.SucursalId == sucursalId);
        return await q.ToDictionaryAsync(c => c.Id);
    }

    private async Task<IReadOnlyList<AsientoLinea>> GenerarConEventoAsync(
        string evento, int sucursalId, decimal montoTotal, decimal montoSubtotal, decimal montoIva)
    {
        var reglas = await db.ReglasAsiento
            .Where(r => r.Evento == evento && r.Activo)
            .OrderBy(r => r.Orden)
            .ToListAsync();

        var cuentasPorId = await CargarCuentasAsync(reglas.Select(r => r.CuentaId), sucursalId);

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

            if (!cuentasPorId.TryGetValue(r.CuentaId, out var cuenta)) continue;

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

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IDashboardService"/>. Concentra las consultas que antes
/// estaban en el <c>@code</c> de Home.razor contra <c>ErpDbContext</c>: la página ahora
/// solo compone KPIs y renderiza.
/// </summary>
public sealed class DashboardService(ErpDbContext db) : IDashboardService
{
    /// <summary>Días de historia que muestra la gráfica del panel.</summary>
    private const int DiasSerie = 14;

    public async Task<DashboardVentasDto> VentasMesAsync(
        int sucursalId, DateTime hoyUtc, CancellationToken ct = default)
    {
        // Kind=Utc para poder comparar contra facturas.fecha_emision (timestamptz);
        // un DateTime con Kind=Unspecified hace que Npgsql lance ArgumentException.
        var inicioMes = new DateTime(hoyUtc.Year, hoyUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var ventas = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId
                && f.FechaEmision >= inicioMes
                && f.Estado != EstadoFactura.Cancelada)
            .Select(f => new { f.FechaEmision, f.Total })
            .ToListAsync(ct);

        var serie = Enumerable.Range(0, DiasSerie)
            .Select(i => hoyUtc.Date.AddDays(-(DiasSerie - 1) + i))
            .Select(d => new DashboardPuntoDto(d, ventas
                .Where(f => DateOnly.FromDateTime(f.FechaEmision) == DateOnly.FromDateTime(d))
                .Sum(f => f.Total)))
            .ToList();

        return new DashboardVentasDto(ventas.Sum(f => f.Total), inicioMes, serie);
    }

    public async Task<DashboardClientesDto> ClientesMesAsync(
        int sucursalId, DateTime inicioMesUtc, CancellationToken ct = default)
    {
        var activos = await db.Clientes.CountAsync(c => c.Activo, ct);
        var atendidos = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId
                && f.FechaEmision >= inicioMesUtc
                && f.Estado != EstadoFactura.Cancelada)
            .Select(f => f.ClienteId)
            .Distinct()
            .CountAsync(ct);

        return new DashboardClientesDto(activos, atendidos, Math.Max(0, activos - atendidos));
    }

    public async Task<List<DashboardStockBajoDto>> StockBajoAsync(
        int sucursalId, CancellationToken ct = default)
    {
        var bajos = await db.InventarioSucursal.AsNoTracking()
            .Where(i => i.SucursalId == sucursalId && i.StockActual < i.StockMinimo)
            .Select(i => new DashboardStockBajoDto(i.Producto!.Nombre, i.StockActual, i.StockMinimo))
            .ToListAsync(ct);

        // Orden en memoria: "Stock - Minimo" no es traducible a SQL de forma portable.
        return bajos.OrderBy(s => s.Stock - s.Minimo).ToList();
    }

    public async Task<List<DashboardActividadDto>> ClientesRecientesAsync(
        int max = 5, CancellationToken ct = default)
    {
        var tops = await db.Clientes.AsNoTracking()
            .Where(c => c.Activo)
            .OrderByDescending(c => c.Id)
            .Take(max)
            .Select(c => new { c.Nombre, c.Rfc })
            .ToListAsync(ct);

        return tops
            .Select(c => new DashboardActividadDto(c.Nombre, c.Rfc ?? "—", "Alta", "#2563EB"))
            .ToList();
    }

    public async Task<List<DashboardActividadDto>> MovimientosRecientesAsync(
        int sucursalId, int max = 5, CancellationToken ct = default)
    {
        var movs = await db.Movimientos.AsNoTracking()
            .Where(m => m.SucursalId == sucursalId)
            .OrderByDescending(m => m.FechaUtc)
            .Take(max)
            .Select(m => new { m.Tipo, m.CantidadLitros, m.Motivo, m.FechaUtc })
            .ToListAsync(ct);

        return movs
            .Select(m => new DashboardActividadDto(
                m.Tipo,
                m.Motivo ?? "—",
                $"{m.CantidadLitros:N2} L · {m.FechaUtc:dd/MM}",
                m.CantidadLitros >= 0 ? "#059669" : "#DC2626"))
            .ToList();
    }

    public async Task<List<DashboardActividadDto>> FacturasRecientesAsync(
        int sucursalId, int max = 5, CancellationToken ct = default)
    {
        var facs = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId && f.Estado != EstadoFactura.Cancelada)
            .OrderByDescending(f => f.FechaEmision)
            .Take(max)
            .Select(f => new
            {
                f.FolioInterno,
                f.Total,
                f.FechaEmision,
                Cliente = f.Cliente!.Nombre,
                Estado = f.Estado.ToString()
            })
            .ToListAsync(ct);

        return facs
            .Select(f => new DashboardActividadDto(
                f.FolioInterno,
                f.Cliente,
                $"${f.Total:N2} · {f.FechaEmision:dd/MM}",
                "#059669"))
            .ToList();
    }
}

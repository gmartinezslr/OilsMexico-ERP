using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class GestionService(ErpDbContext db, ISucursalContext ctx) : IGestionService
{
    public async Task<DashboardGestionDto> DashboardAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        // Una sola ronda: facturas del periodo + detalles con join a Productos para el costo.
        // Evita N consultas con .Result (FindAsync().Result bloquea y dispara
        // "A second operation was started on this context instance").
        var filas = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId
                && f.FechaEmision >= desdeUtc && f.FechaEmision < hastaUtc
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .Select(f => new
            {
                f.Subtotal,
                f.Iva,
                f.Total,
                Detalles = f.Detalles.Select(d => new
                {
                    d.Cantidad,
                    PrecioCosto = d.Producto != null ? d.Producto.PrecioCosto : 0m
                }).ToList()
            })
            .ToListAsync(ct);

        var ingresos = filas.Sum(f => f.Subtotal);
        var iva = filas.Sum(f => f.Iva);
        var totalVentas = filas.Sum(f => f.Total);
        var numVentas = filas.Count;

        var unidadesVendidas = filas.SelectMany(f => f.Detalles).Sum(d => d.Cantidad);
        var montoCosto = filas.SelectMany(f => f.Detalles).Sum(d => d.Cantidad * d.PrecioCosto);
        var utilidad = Math.Round(totalVentas - montoCosto, 2);

        var promedio = numVentas > 0 ? Math.Round(totalVentas / numVentas, 2) : 0m;
        var dias = (hasta - desde).Days;
        var ventasPorDia = numVentas / Math.Max(1, dias);

        var totalCortes = await db.CortesCaja.AsNoTracking()
            .Where(c => c.SucursalId == sucursalId && c.Estado == "Abierto")
            .CountAsync(ct);

        return new DashboardGestionDto(
            ingresos, iva, totalVentas, numVentas, promedio, unidadesVendidas, montoCosto, utilidad,
            ventasPorDia, totalCortes);
    }

    public async Task<List<RotacionABCDto>> RotacionAbcAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var ventas = await db.FacturaDetalles.AsNoTracking()
            .Join(db.Facturas.AsNoTracking(), d => d.FacturaId, f => f.Id, (d, f) => new { d, f })
            .Where(x => x.f.SucursalId == sucursalId
                && x.f.FechaEmision >= desdeUtc && x.f.FechaEmision < hastaUtc
                && x.f.Estado != EstadoFactura.Cancelada && x.f.Estado != EstadoFactura.Devolucion)
            .GroupBy(x => x.d.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Cantidad = g.Sum(x => x.d.Cantidad),
                Importe = g.Sum(x => x.d.Importe)
            })
            .OrderByDescending(x => x.Importe)
            .ToListAsync(ct);

        var productos = await db.Productos.AsNoTracking()
            .Where(p => ventas.Select(v => v.ProductoId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nombre ?? $"Prod #{p.Id}");

        var total = ventas.Sum(v => v.Importe);
        var acumulado = 0m;
        var resultado = new List<RotacionABCDto>();

        foreach (var v in ventas)
        {
            acumulado += v.Importe;
            var clase = acumulado / Math.Max(1m, total) <= 0.30m ? "A"
                        : acumulado / Math.Max(1m, total) <= 0.70m ? "B" : "C";
            resultado.Add(new RotacionABCDto(
                v.ProductoId, productos.GetValueOrDefault(v.ProductoId) ?? $"Prod #{v.ProductoId}",
                v.Cantidad, Math.Round(v.Importe, 2), Math.Round(acumulado / Math.Max(1m, total) * 100, 1), clase));
        }

        return resultado;
    }

    public async Task<List<CorteSucursalDto>> CortesPorSucursalAsync(int sucursalId, CancellationToken ct = default)
    {
        var idsSucursal = ctx.Rol == "Admin"
            ? await db.Sucursales.AsNoTracking().Select(s => s.Id).ToListAsync(ct)
            : new List<int> { sucursalId };

        var ventas = await db.Facturas.AsNoTracking()
            .Where(f => idsSucursal.Contains(f.SucursalId)
                // sin filtro de fecha -> totales acumulados
                // sin filtro de fecha -> totales acumulados
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .GroupBy(f => f.SucursalId)
            .Select(g => new
            {
                SucursalId = g.Key,
                Total = g.Sum(f => f.Total),
                NumVentas = g.Count()
            })
            .ToListAsync(ct);

        var sucursales = await db.Sucursales.AsNoTracking()
            .Where(s => idsSucursal.Contains(s.Id))
            .Select(s => new { s.Id, s.Nombre })
            .ToListAsync(ct);

        return sucursales.Select(s => new CorteSucursalDto(
            s.Id, s.Nombre ?? $"Sucursal {s.Id}",
            ventas.FirstOrDefault(v => v.SucursalId == s.Id)?.Total ?? 0m,
            ventas.FirstOrDefault(v => v.SucursalId == s.Id)?.NumVentas ?? 0)).ToList();
    }

    public async Task<List<ViscosidadDto>> InventarioPorViscosidadAsync(int sucursalId, CancellationToken ct = default)
    {
        var filas = await (from inv in db.InventarioSucursal.AsNoTracking()
                           join p in db.Productos.AsNoTracking() on inv.ProductoId equals p.Id
                           where inv.SucursalId == sucursalId && inv.StockActual > 0
                           group inv by p.Viscosidad into g
                           select new
                           {
                               Viscosidad = g.Key,
                               Stock = g.Sum(x => x.StockActual),
                               Productos = g.Count()
                           })
            .OrderByDescending(x => x.Stock)
            .ToListAsync(ct);

        return filas.Select(x => new ViscosidadDto(x.Viscosidad!, x.Stock, x.Productos)).ToList();
    }

    public async Task<List<VentasPorProductoDto>> VentasPorProductoAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var filas = await (from d in db.FacturaDetalles.AsNoTracking()
                           join f in db.Facturas.AsNoTracking() on d.FacturaId equals f.Id
                           where f.SucursalId == sucursalId
                                 && f.FechaEmision >= desdeUtc && f.FechaEmision < hastaUtc
                                 && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion
                           group d by new { d.ProductoId } into g
                           select new
                           {
                               ProductoId = g.Key.ProductoId,
                               Cantidad = g.Sum(x => x.Cantidad),
                               Importe = g.Sum(x => x.Importe)
                           })
            .OrderByDescending(x => x.Importe)
            .ToListAsync(ct);

        return filas.Select(x => new VentasPorProductoDto(x.ProductoId, x.Cantidad, x.Importe)).ToList();
    }

    public async Task<List<MarcaProductoDto>> VentasPorMarcaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var filas = await (from d in db.FacturaDetalles.AsNoTracking()
                           join f in db.Facturas.AsNoTracking() on d.FacturaId equals f.Id
                           where f.SucursalId == sucursalId
                                 && f.FechaEmision >= desdeUtc && f.FechaEmision < hastaUtc
                                 && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion
                           join p in db.Productos.AsNoTracking() on d.ProductoId equals p.Id
                           where p.Marca != null
                           group d by p.Marca into g
                           select new
                           {
                               Marca = g.Key,
                               Cantidad = g.Sum(x => x.Cantidad),
                               Importe = g.Sum(x => x.Importe)
                           })
            .OrderByDescending(x => x.Importe)
            .ToListAsync(ct);

        return filas.Select(x => new MarcaProductoDto(x.Marca!, x.Cantidad, x.Importe)).ToList();
    }


    public async Task<List<VentasPorDiaDto>> VentasPorDiaAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var grupos = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId
                && f.FechaEmision >= desdeUtc && f.FechaEmision < hastaUtc
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .GroupBy(f => f.FechaEmision.Date)
            .Select(g => new { Fecha = g.Key, Total = g.Sum(f => f.Total), Cantidad = g.Count() })
            .OrderBy(x => x.Fecha)
            .ToListAsync(ct);

        return grupos.Select(g => new VentasPorDiaDto(
            g.Fecha, g.Total, g.Cantidad)).ToList();
    }
}


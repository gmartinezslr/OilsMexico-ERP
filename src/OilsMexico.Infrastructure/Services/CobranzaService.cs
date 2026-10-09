using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Motor de cobranza efectiva (cash basis). Única fuente de verdad de "cuánto dinero entró",
/// a quién se le acreditó y cuántos litros corresponden. Ver <see cref="ICobranzaService"/>.
/// </summary>
public sealed partial class CobranzaService(ErpDbContext db) : ICobranzaService
{
    /// <summary>Cobros de un periodo ya agregados por factura (proyectable por EF y pasable a helpers).</summary>
    private sealed record CobroAgrupado(int FacturaId, decimal Monto, int N, DateTime Fecha);

    public async Task<List<CobranzaResumenDto>> CobranzaPorVendedorAsync(
        int? sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var detalle = await DetalleCobranzaAsync(null, desde, hasta, sucursalId, ct);
        return Agrupar(detalle);
    }

    public async Task<CobranzaResumenDto> CobranzaVendedorAsync(
        int vendedorId, DateTime desde, DateTime hasta, int? sucursalId = null, CancellationToken ct = default)
    {
        var detalle = await DetalleCobranzaAsync(vendedorId, desde, hasta, sucursalId, ct);
        var resumen = Agrupar(detalle).FirstOrDefault(r => r.VendedorId == vendedorId);
        return resumen ?? new CobranzaResumenDto(vendedorId, $"Vendedor #{vendedorId}", 0m, 0m, 0m, 0, 0);
    }

    public async Task<List<CobranzaFacturaDto>> DetalleCobranzaAsync(
        int? vendedorId, DateTime desde, DateTime hasta, int? sucursalId = null, CancellationToken ct = default)
    {
        // Mismo criterio de rango que GestionService: fechas locales -> UTC, tope exclusivo.
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        // 1) Sólo el dinero realmente ingresado EN EL PERIODO, agrupado por factura.
        //    PUE y PPD quedan unificados aquí porque toda venta PUE genera su cobro automático.
        var cobros = await db.VentaCobros.AsNoTracking()
            .Where(c => c.FechaPagoUtc >= desdeUtc && c.FechaPagoUtc < hastaUtc
                && (sucursalId == null || c.SucursalId == sucursalId))
            .GroupBy(c => c.FacturaId)
            .Select(g => new CobroAgrupado(
                g.Key, g.Sum(x => x.Monto), g.Count(), g.Min(x => x.FechaPagoUtc)))
            .ToListAsync(ct);
        if (cobros.Count == 0) return [];

        var porFactura = cobros.ToDictionary(x => x.FacturaId);

        // 2) Las facturas que aportaron cobro, con sus renglones. Canceladas y devoluciones quedan
        //    fuera aunque tengan cobro registrado: no se paga comisión sobre una venta revertida.
        var facturas = await db.Facturas.AsNoTracking()
            .Include(f => f.Detalles).ThenInclude(d => d.Producto)
            .Include(f => f.Cliente)
            .Where(f => porFactura.Keys.Contains(f.Id)
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion
                && (vendedorId == null || f.VendedorId == vendedorId))
            .ToListAsync(ct);
        if (facturas.Count == 0) return [];

        var vendedorIds = facturas
            .Select(f => f.VendedorId).Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var nombres = vendedorIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Usuarios.AsNoTracking().Where(u => vendedorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        return Prorratear(facturas, porFactura, nombres);
    }

    /// <summary>
    /// Núcleo del cálculo: reparte el dinero cobrado EN EL PERIODO sobre los renglones de la factura.
    ///  - fraccion   = min(cobrado del periodo / total de la factura, 1)  → un pago parcial no libera el total.
    ///  - factorNeto = subtotal / total de ESA factura                      → base sin IVA exacta (soporta IVA 0%).
    ///  - se prorratea renglón por renglón para que el tabulador por presentación (fase 3) tenga
    ///    importe neto y litros POR PRODUCTO, no sólo un total agregado.
    /// </summary>
    private static List<CobranzaFacturaDto> Prorratear(
        List<Factura> facturas,
        Dictionary<int, CobroAgrupado> porFactura,
        Dictionary<int, string> nombres)
    {
        var res = new List<CobranzaFacturaDto>();
        foreach (var f in facturas)
        {
            var cobro = porFactura[f.Id];
            if (f.Total <= 0m || cobro.Monto <= 0m) continue;

            var fraccion = Math.Min(cobro.Monto / f.Total, 1m);
            var factorNeto = f.Subtotal / f.Total;

            decimal netoCobrado = 0m, litrosCobrados = 0m;
            var renglones = new List<CobranzaRenglonDto>(f.Detalles.Count);
            foreach (var d in f.Detalles)
            {
                // Importe YA viene CON IVA (VentasService: importe = cantidad * precio y luego
                // Subtotal = Σimportes / (1+TasaIva)), así que NO se vuelve a dividir por 1.16:
                // hacerlo quitaba el IVA dos veces y subestimaba la base de comisión ~14%.
                // La conversión a neto se hace UNA sola vez, aquí, con factorNeto.
                var neto = Math.Round(d.Importe * factorNeto * fraccion, 2);
                var litros = Math.Round(d.LitrosDescontados * fraccion, 2);
                netoCobrado += neto;
                litrosCobrados += litros;
                renglones.Add(new CobranzaRenglonDto(
                    d.ProductoId,
                    d.Producto?.Nombre ?? $"Producto #{d.ProductoId}",
                    d.UnidadNombre,
                    d.Cantidad,
                    d.LitrosDescontados,
                    d.Importe,
                    neto,
                    litros));
            }

            res.Add(new CobranzaFacturaDto(
                f.Id, f.FolioInterno, cobro.Fecha,
                f.ClienteId, f.Cliente?.Nombre ?? $"Cliente #{f.ClienteId}",
                f.VendedorId,
                f.VendedorId is int vid && nombres.TryGetValue(vid, out var n) ? n : "(sin atribución)",
                f.MetodoPagoSat,
                cobro.N,
                f.Total,
                Math.Round(f.Subtotal, 2),
                Math.Round(cobro.Monto, 2),
                Math.Round(fraccion, 6),
                Math.Round(netoCobrado, 2),
                Math.Round(litrosCobrados, 2),
                renglones));
        }
        return res.OrderBy(x => x.FechaCobro).ToList();
    }

    private static List<CobranzaResumenDto> Agrupar(List<CobranzaFacturaDto> detalle) =>
        detalle
            .GroupBy(f => new { f.VendedorId, f.Vendedor })
            .Select(g => new CobranzaResumenDto(
                g.Key.VendedorId,
                g.Key.Vendedor,
                Math.Round(g.Sum(x => x.CobradoEnPeriodo), 2),
                Math.Round(g.Sum(x => x.NetoCobradoSinIva), 2),
                Math.Round(g.Sum(x => x.LitrosCobrados), 2),
                g.Count(),
                g.Sum(x => x.CobrosEnPeriodo)))
            .OrderByDescending(x => x.NetoCobradoSinIva)
            .ToList();
}

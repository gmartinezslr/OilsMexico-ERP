using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// «Comisiones potenciales»: lo que un vendedor liberaría si lograra cobrar.
/// NO es cobranza ni entra al cálculo de comisiones — es el incentivo de cobranza del portal.
/// </summary>
public sealed partial class CobranzaService
{
    public async Task<List<PotencialDto>> PotencialesAsync(
        int vendedorId, DateTime desde, DateTime hasta,
        int? sucursalId = null, CancellationToken ct = default)
    {
        var desdeUtc = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var hastaUtc = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var hoy = DateTime.Today;

        // Facturas VIVAS del vendedor emitidas en el periodo. Las PUE nunca deben aparecer aquí
        // (ya se cobraron solas al emitirse); las PPD con pago parcial aparecen por lo que falta.
        var facturas = await db.Facturas.AsNoTracking()
            .Include(f => f.Detalles).ThenInclude(d => d.Producto)
            .Include(f => f.Cliente)
            .Where(f => f.VendedorId == vendedorId
                && f.FechaEmision >= desdeUtc && f.FechaEmision < hastaUtc
                && f.MetodoPagoSat == "PPD"                       // sólo lo que se cobra aparte
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion
                && (sucursalId == null || f.SucursalId == sucursalId))
            .ToListAsync(ct);
        if (facturas.Count == 0) return [];

        // Lo ya cobrado por cada factura, para quedarnos con el saldo real pendiente.
        var cobrado = await db.VentaCobros.AsNoTracking()
            .Where(c => facturas.Select(f => f.Id).Contains(c.FacturaId))
            .GroupBy(c => c.FacturaId)
            .Select(g => new { FacturaId = g.Key, Monto = g.Sum(x => x.Monto) })
            .ToDictionaryAsync(x => x.FacturaId, x => x.Monto, ct);

        // El % que le toca a cada presentación, para estimar la comisión latente.
        var productoIds = facturas.SelectMany(f => f.Detalles).Select(d => d.ProductoId).Distinct().ToList();
        var pct = await db.UnidadesMedida.AsNoTracking()
            .Where(u => productoIds.Contains(u.ProductoId))
            .Select(u => new { u.ProductoId, u.UnidadNombre, u.PorcComisionBono, u.PorcComisionBase })
            .ToListAsync(ct);
        var tabulador = pct.ToDictionary(
            x => (x.ProductoId, x.UnidadNombre),
            x => Math.Max(x.PorcComisionBono, x.PorcComisionBase));

        var salida = new List<PotencialDto>();
        foreach (var f in facturas)
        {
            var pagado = cobrado.TryGetValue(f.Id, out var p) ? p : 0m;
            var pendiente = f.Total - pagado;
            if (pendiente <= 0.005m) continue;   // ya se cobró toda: no hay potencial

            // El saldo se reparte con la MISMA proporción que el resto del motor, así que la
            // comisión potencial es coherente con la que se pagaría al caer el cobro.
            var factor = f.Total > 0 ? pendiente / f.Total : 0m;
            var netoPendiente = Math.Round(f.Subtotal * factor, 2);
            var litrosPendientes = Math.Round(
                f.Detalles.Sum(d => d.LitrosDescontados) * factor, 2);

            var comPotencial = f.Detalles.Sum(d =>
            {
                var llave = (d.ProductoId, d.UnidadNombre);
                var p = tabulador.TryGetValue(llave, out var porc) ? porc : 0m;
                var netoRenglon = Math.Round(d.Importe * factor, 2);
                return Math.Round(netoRenglon * p / 100m, 2);
            });

            salida.Add(new PotencialDto(
                f.Id, f.FolioInterno, f.Cliente?.Nombre ?? "(sin cliente)",
                f.FechaEmision.ToLocalTime().Date, f.Total, pendiente,
                netoPendiente, litrosPendientes,
                Math.Max(0, (hoy - f.FechaEmision.ToLocalTime().Date).Days),
                Math.Round(comPotencial, 2)));
        }

        // Lo más urgente primero: lleva más días parado.
        return salida.OrderByDescending(x => x.DiasEmitida).ToList();
    }
}

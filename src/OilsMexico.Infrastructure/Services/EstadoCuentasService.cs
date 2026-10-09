using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class EstadoCuentasService(ErpDbContext db, ISucursalContext ctx) : IEstadoCuentasService
{
    public async Task<List<EstadoCuentaClienteDto>> ListarEstadosCuentasAsync(int sucursalId, CancellationToken ct = default)
    {
        var facturasPorCliente = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == sucursalId
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .GroupBy(f => f.ClienteId)
            .Select(g => new { ClienteId = g.Key, Total = g.Sum(f => f.Total) })
            .ToDictionaryAsync(x => x.ClienteId, x => x.Total, ct);

        if (facturasPorCliente.Count == 0) return [];

        var clienteIds = facturasPorCliente.Keys.ToList();

        var cobrosPorCliente = await db.VentaCobros.AsNoTracking()
            .Where(v => v.SucursalId == sucursalId && clienteIds.Contains(v.ClienteId)
                // Mismo criterio cash-basis que CobranzaService: el cobro de una factura cancelada
                // o devuelta NO es "dinero entrado" (se reembolsó), así que no compensa deuda.
                && (v.Factura == null
                    || (v.Factura.Estado != EstadoFactura.Cancelada
                        && v.Factura.Estado != EstadoFactura.Devolucion)))
            .GroupBy(v => v.ClienteId)
            .Select(g => new { ClienteId = g.Key, Total = g.Sum(v => v.Monto) })
            .ToDictionaryAsync(x => x.ClienteId, x => x.Total, ct);

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => clienteIds.Contains(c.Id) && c.Activo)
            .ToListAsync(ct);

        var list = new List<EstadoCuentaClienteDto>();
        foreach (var c in clientes)
        {
            var totalFac = facturasPorCliente.GetValueOrDefault(c.Id, 0m);
            var totalCob = cobrosPorCliente.GetValueOrDefault(c.Id, 0m);
            var saldo = Math.Round(totalFac - totalCob, 2);
            if (totalFac > 0.01m)
            {
                list.Add(new EstadoCuentaClienteDto(
                    c.Id, c.Nombre, c.Rfc, c.Telefono ?? string.Empty, c.Email ?? string.Empty,
                    totalFac, totalCob, saldo));
            }
        }

        return list.OrderByDescending(x => x.TotalFacturasAbiertas).ToList();
    }

    public async Task<ClienteEstadoCuentaDto?> ObtenerEstadoCuentaAsync(int clienteId, CancellationToken ct = default)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clienteId, ct);
        if (cliente is null) return null;

        var sucursalId = ctx.SucursalId;

        var facturasAbiertas = await db.Facturas.AsNoTracking()
            .Where(f => f.ClienteId == clienteId
                && (sucursalId == 0 || f.SucursalId == sucursalId)
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion)
            .OrderByDescending(f => f.FechaEmision)
            .Select(f => new FacturaEstadoCuentaDto(
                f.Id, f.FolioInterno, f.FechaEmision, f.Estado.ToString(), f.Total, f.UuidSat))
            .ToListAsync(ct);

        var pagos = await db.VentaCobros.AsNoTracking()
            .Where(v => v.ClienteId == clienteId && (sucursalId == 0 || v.SucursalId == sucursalId)
                // Mismo criterio cash-basis que CobranzaService (ver ListarEstadosCuentasAsync).
                && (v.Factura == null
                    || (v.Factura.Estado != EstadoFactura.Cancelada
                        && v.Factura.Estado != EstadoFactura.Devolucion)))
            .OrderByDescending(v => v.FechaPagoUtc)
            .Select(v => new PagoCuentaDto(
                v.Id, v.FechaPagoUtc, "VentaCobro", v.Monto, v.Referencia ?? string.Empty, null))
            .ToListAsync(ct);

        var totalAbiertas = facturasAbiertas.Sum(f => f.Total);
        var totalPagos = pagos.Sum(p => p.Monto);
        var saldo = Math.Round(totalAbiertas - totalPagos, 2);

        if (saldo <= 0.01m && facturasAbiertas.Count == 0) return null;

        return new ClienteEstadoCuentaDto(
            cliente.Id, cliente.Nombre, cliente.Rfc, cliente.Telefono ?? string.Empty, cliente.Email ?? string.Empty,
            facturasAbiertas, pagos, saldo);
    }
}

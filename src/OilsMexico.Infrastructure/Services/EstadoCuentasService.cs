using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class EstadoCuentasService(ErpDbContext db, ISucursalContext ctx) : IEstadoCuentasService
{
    public async Task<List<EstadoCuentaClienteDto>> ListarEstadosCuentasAsync(int sucursalId, CancellationToken ct = default)
    {
        var q = db.Clientes.AsNoTracking()
            .Where(c => c.SucursalId == sucursalId)
            .Where(c => c.Activo)
            .Select(c => new
            {
                c.Id, c.Nombre, c.Rfc, c.Telefono, c.Email,
                TotalFacturas = c.Facturas.Where(f => f.SucursalId == sucursalId
                        && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
                    .Sum(f => f.Total),
                TotalPagos = c.VentaCobros.Where(v => v.SucursalId == sucursalId).Sum(v => v.Monto)
            })
            .Where(x => x.TotalFacturas > 0.01m)
            .OrderByDescending(x => x.TotalFacturas)
            .ToListAsync(ct);

        return q.Select(x => new EstadoCuentaClienteDto(
            x.Id, x.Nombre, x.Rfc, x.Telefono ?? string.Empty, x.Email ?? string.Empty,
            x.TotalFacturas, x.TotalPagos, Math.Round(x.TotalFacturas - x.TotalPagos, 2)))
            .ToList();
    }

    public Task<ClienteEstadoCuentaDto?> ObtenerEstadoCuentaAsync(int clienteId, CancellationToken ct = default)
    {
        var cliente = db.Clientes.AsNoTracking().FirstOrDefault(x => x.Id == clienteId);
        if (cliente is null) return Task.FromResult<ClienteEstadoCuentaDto?>(null);

        var facturasAbiertas = cliente.Facturas
            .Where(f => f.SucursalId == cliente.SucursalId
                && f.Estado != EstadoFactura.Cancelada
                && f.Estado != EstadoFactura.Devolucion)
            .Select(f => new FacturaEstadoCuentaDto(
                f.Id, f.FolioInterno, f.FechaEmision, f.Estado.ToString(), f.Total, f.UuidSat))
            .OrderByDescending(f => f.FechaEmision)
            .ToList();

        var pagos = db.VentaCobros
            .Where(v => v.SucursalId == cliente.SucursalId && v.ClienteId == clienteId)
            .Select(v => new PagoCuentaDto(
                v.Id, v.FechaPagoUtc, "VentaCobro", v.Monto, v.Referencia, null))
            .OrderByDescending(v => v.Fecha)
            .ToList();

        var totalAbiertas = facturasAbiertas.Sum(f => f.Total);
        var totalPagos = pagos.Sum(p => p.Monto);
        var saldo = Math.Round(totalAbiertas - totalPagos, 2);

        return Task.FromResult<ClienteEstadoCuentaDto?>(saldo <= 0.01m ? null : new ClienteEstadoCuentaDto(
            cliente.Id, cliente.Nombre, cliente.Rfc, cliente.Telefono ?? string.Empty, cliente.Email ?? string.Empty,
            facturasAbiertas, pagos, saldo));
    }
}

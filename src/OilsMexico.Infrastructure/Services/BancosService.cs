using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Conciliación bancaria: captura el extracto del banco y lo empata con los cobros
/// (VentaCobro, Abono) y pagos a proveedor (CompraPago, Cargo) que ya registra el ERP.
/// Cierra el hueco «los pagos/cobros existen pero no la operación bancaria».
/// </summary>
public sealed partial class BancosService(ErpDbContext db, ISucursalContext ctx) : IBancosService
{
    public async Task<List<CuentaBancariaDto>> ListarCuentasAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var cuentas = await db.CuentasBancarias.AsNoTracking()
            .Where(c => c.SucursalId == suc)
            .OrderBy(c => c.Banco).ThenBy(c => c.Alias)
            .ToListAsync(ct);
        if (cuentas.Count == 0) return [];

        var ids = cuentas.Select(c => c.Id).ToList();
        // Agregados del extracto por cuenta: pendientes, conciliados y saldo calculado.
        var agg = await db.MovimientosBancarios.AsNoTracking()
            .Where(m => ids.Contains(m.CuentaBancariaId))
            .GroupBy(m => m.CuentaBancariaId)
            .Select(g => new
            {
                CuentaId = g.Key,
                Pendientes = g.Count(m => m.Estado == EstadoConciliacion.Pendiente),
                Conciliados = g.Count(m => m.Estado == EstadoConciliacion.Conciliado),
                Abonos = g.Where(m => m.Naturaleza == NaturalezaMovimiento.Abono).Sum(m => m.Monto),
                Cargos = g.Where(m => m.Naturaleza == NaturalezaMovimiento.Cargo).Sum(m => m.Monto)
            })
            .ToListAsync(ct);

        var porCuenta = agg.ToDictionary(a => a.CuentaId);
        return cuentas.Select(c =>
        {
            porCuenta.TryGetValue(c.Id, out var a);
            var saldo = c.SaldoInicial + (a?.Abonos ?? 0m) - (a?.Cargos ?? 0m);
            return new CuentaBancariaDto(
                c.Id, c.SucursalId, c.Banco, c.Alias, c.NumeroCuenta, c.Clabe, c.Moneda,
                c.SaldoInicial, c.Activa, a?.Pendientes ?? 0, a?.Conciliados ?? 0,
                Math.Round(saldo, 2));
        }).ToList();
    }

    public async Task<CuentaBancariaDto> CrearCuentaAsync(CrearCuentaBancariaRequest request, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(request.SucursalId);
        if (string.IsNullOrWhiteSpace(request.Banco)) throw new InvalidOperationException("El banco es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Alias)) throw new InvalidOperationException("El alias de la cuenta es obligatorio.");
        if (request.SaldoInicial < 0) throw new InvalidOperationException("El saldo inicial no puede ser negativo.");

        var cuenta = new CuentaBancaria
        {
            SucursalId = suc,
            Banco = request.Banco.Trim(),
            Alias = request.Alias.Trim(),
            NumeroCuenta = string.IsNullOrWhiteSpace(request.NumeroCuenta) ? null : request.NumeroCuenta.Trim(),
            Clabe = string.IsNullOrWhiteSpace(request.Clabe) ? null : request.Clabe.Trim(),
            Moneda = string.IsNullOrWhiteSpace(request.Moneda) ? "MXN" : request.Moneda.Trim().ToUpperInvariant(),
            SaldoInicial = Math.Round(request.SaldoInicial, 2),
            Activa = true
        };
        db.CuentasBancarias.Add(cuenta);
        await db.SaveChangesAsync(ct);
        return new CuentaBancariaDto(
            cuenta.Id, cuenta.SucursalId, cuenta.Banco, cuenta.Alias, cuenta.NumeroCuenta,
            cuenta.Clabe, cuenta.Moneda, cuenta.SaldoInicial, cuenta.Activa, 0, 0, cuenta.SaldoInicial);
    }

    public async Task<List<MovimientoBancarioDto>> ListarMovimientosAsync(int cuentaId, EstadoConciliacion? estado, CancellationToken ct = default)
    {
        var cuenta = await db.CuentasBancarias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cuentaId, ct)
            ?? throw new InvalidOperationException("La cuenta bancaria no existe.");
        if (!ctx.EsAdmin && cuenta.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes ver movimientos de otra sucursal.");

        var q = db.MovimientosBancarios.AsNoTracking().Where(m => m.CuentaBancariaId == cuentaId);
        if (estado.HasValue) q = q.Where(m => m.Estado == estado.Value);

        var movs = await q.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToListAsync(ct);

        // Etiqueta legible de con qué se concilió (folio del cobro / pago).
        var cobroIds = movs.Where(m => m.VentaCobroId is not null).Select(m => m.VentaCobroId!.Value).Distinct().ToList();
        var pagoIds = movs.Where(m => m.CompraPagoId is not null).Select(m => m.CompraPagoId!.Value).Distinct().ToList();

        var cobroEtiqueta = await db.VentaCobros.AsNoTracking()
            .Where(v => cobroIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Factura != null ? $"Cobro {v.Factura.FolioInterno}" : $"Cobro #{v.Id}", ct);
        var pagoEtiqueta = await db.CompraPagos.AsNoTracking()
            .Where(p => pagoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Compra != null ? $"Pago {p.Compra.FolioInterno}" : $"Pago #{p.Id}", ct);

        return movs.Select(m => new MovimientoBancarioDto(
            m.Id, m.CuentaBancariaId, m.Fecha, m.Concepto, m.Referencia, m.Naturaleza,
            m.Monto, m.Estado, m.VentaCobroId, m.CompraPagoId, Etiqueta(m, cobroEtiqueta, pagoEtiqueta), m.ConciliadoUtc)
        ).ToList();
    }

    public async Task<MovimientoBancarioDto> CargarMovimientoAsync(CargarMovimientoRequest request, CancellationToken ct = default)
    {
        var cuenta = await db.CuentasBancarias.FirstOrDefaultAsync(c => c.Id == request.CuentaBancariaId, ct)
            ?? throw new InvalidOperationException("La cuenta bancaria no existe.");
        if (!ctx.EsAdmin && cuenta.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cargar movimientos de otra sucursal.");
        if (!cuenta.Activa) throw new InvalidOperationException("La cuenta está inactiva.");
        if (request.Monto <= 0) throw new InvalidOperationException("El monto debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(request.Concepto)) throw new InvalidOperationException("El concepto es obligatorio.");

        var mov = new MovimientoBancario
        {
            CuentaBancariaId = cuenta.Id,
            Fecha = request.Fecha.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.Fecha, DateTimeKind.Utc) : request.Fecha.ToUniversalTime(),
            Concepto = request.Concepto.Trim(),
            Referencia = string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim(),
            Naturaleza = request.Naturaleza,
            Monto = Math.Round(request.Monto, 2),
            Estado = EstadoConciliacion.Pendiente
        };
        db.MovimientosBancarios.Add(mov);
        await db.SaveChangesAsync(ct);
        return new MovimientoBancarioDto(
            mov.Id, mov.CuentaBancariaId, mov.Fecha, mov.Concepto, mov.Referencia,
            mov.Naturaleza, mov.Monto, mov.Estado, null, null, null, null);
    }

    public async Task<List<MovimientoErpDto>> MovimientosErpPendientesAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var resultado = new List<MovimientoErpDto>();

        // Cobros (VentaCobro) de la sucursal que aún no están empatados con ningún renglón del extracto.
        var cobrosConciliados = db.MovimientosBancarios.AsNoTracking()
            .Where(m => m.VentaCobroId != null).Select(m => m.VentaCobroId!.Value);
        var cobros = await db.VentaCobros.AsNoTracking()
            .Where(v => v.SucursalId == suc && !cobrosConciliados.Contains(v.Id))
            .OrderByDescending(v => v.FechaPagoUtc)
            .Select(v => new MovimientoErpDto(
                "Cobro", v.Id, v.FechaPagoUtc,
                v.Factura != null ? v.Factura.Cliente!.Nombre : string.Empty,
                v.Referencia, v.Monto))
            .ToListAsync(ct);
        resultado.AddRange(cobros);

        // Pagos a proveedor (CompraPago) cuya compra es de la sucursal y no están empatados.
        var pagosConciliados = db.MovimientosBancarios.AsNoTracking()
            .Where(m => m.CompraPagoId != null).Select(m => m.CompraPagoId!.Value);
        var pagos = await db.CompraPagos.AsNoTracking()
            .Where(p => p.Compra!.SucursalId == suc && !pagosConciliados.Contains(p.Id))
            .OrderByDescending(p => p.FechaUtc)
            .Select(p => new MovimientoErpDto(
                "Pago", p.Id, p.FechaUtc,
                p.Compra!.Proveedor!.Nombre, p.Referencia, p.Monto))
            .ToListAsync(ct);
        resultado.AddRange(pagos);

        return resultado.OrderByDescending(x => x.Fecha).ToList();
    }

    private static string? Etiqueta(MovimientoBancario m,
        Dictionary<int, string> cobros, Dictionary<int, string> pagos)
    {
        if (m.VentaCobroId is int ci && cobros.TryGetValue(ci, out var ec)) return ec;
        if (m.CompraPagoId is int pi && pagos.TryGetValue(pi, out var ep)) return ep;
        return null;
    }
}

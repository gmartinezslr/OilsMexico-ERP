using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CuentasContablesService(ErpDbContext db, ISucursalContext ctx) : ICuentasContablesService
{
    private static readonly List<CuentaContable> _catalogo = InicializarCatalogo();

    public Task<List<CuentaContableDto>> ListarCuentasAsync(int sucursalId, CancellationToken ct = default)
    {
        var q = db.CuentasContables.AsNoTracking().Where(c => c.SucursalId == sucursalId);
        return q.OrderBy(c => c.Codigo)
            .Select(c => new CuentaContableDto(c.Id, c.Codigo, c.Nombre, c.Tipo, c.Descripcion, c.SaldoActual))
            .ToListAsync(ct);
    }

    public Task<CuentaContableDto?> ObtenerCuentaAsync(int cuentaId, CancellationToken ct = default)
    {
        return db.CuentasContables.AsNoTracking()
            .Where(c => c.SucursalId == ctx.SucursalId && c.Id == cuentaId)
            .Select(c => new CuentaContableDto(c.Id, c.Codigo, c.Nombre, c.Tipo, c.Descripcion, c.SaldoActual))
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<CuentaContableDto>> SaldoPorCuentaAsync(int sucursalId, CancellationToken ct = default)
    {
        return db.CuentasContables.AsNoTracking()
            .Where(c => c.SucursalId == sucursalId)
            .Select(c => new CuentaContableDto(c.Id, c.Codigo, c.Nombre, c.Tipo, c.Descripcion, c.SaldoActual))
            .OrderBy(c => c.Codigo)
            .ToListAsync(ct);
    }

    private static List<CuentaContable> InicializarCatalogo()
    {
        return new List<CuentaContable>
        {
            new() { Codigo = "1000", Nombre = "Efectivo y equivalentes", Tipo = 1, Descripcion = "Efectivo en sucursal" },
            new() { Codigo = "1100", Nombre = "Cuentas por cobrar", Tipo = 1, Descripcion = "Abonos a favor de clientes" },
            new() { Codigo = "1200", Nombre = "Mercancía / Compras", Tipo = 1, Descripcion = "Costo de las compras" },
            new() { Codigo = "2000", Nombre = "Proveedores / Pagar", Tipo = 2, Descripcion = "Deudas con proveedores" },
            new() { Codigo = "3000", Nombre = "Ventas", Tipo = 4, Descripcion = "Ingresos por ventas" },
            new() { Codigo = "3100", Nombre = "Devoluciones y descuentos", Tipo = 4, Descripcion = "Contralado a ventas" },
            new() { Codigo = "4000", Nombre = "IVA a cobrar", Tipo = 2, Descripcion = "IVA a Pagar / a cobrar" },
            new() { Codigo = "6000", Nombre = "Gastos generales", Tipo = 5, Descripcion = "Gastos operativos" }
        };
    }
}

    public async Task<RegistrarAsientoResult> RegistrarAsientoAsync(RegistrarAsientoRequest request, CancellationToken ct = default)
    {
        if (request.Detalles.Count == 0)
            throw new InvalidOperationException("La póliza debe contener al menos un detalle.");

        var detalles = new List<DetalleAsiento>();
        var totalDebitos = 0m;
        var totalCreditos = 0m;

        foreach (var req in request.Detalles)
        {
            var cuenta = await db.CuentasContables.FindAsync([req.CuentaId], ct)
                ?? throw new InvalidOperationException($"La cuenta {req.CuentaId} no existe.");

            if (req.TipoMovimiento != MovimientoTipo.Debito && req.TipoMovimiento != MovimientoTipo.Credito)
                throw new InvalidOperationException("El tipo de movimiento debe ser Débito o Crédito.");

            if (req.Importe <= 0)
                throw new InvalidOperationException("El importe debe ser mayor a cero.");

            var importe = Math.Round(req.Importe, 2);
            detalles.Add(new DetalleAsiento
            {
                CuentaId = req.CuentaId,
                TipoMovimiento = req.TipoMovimiento,
                Importe = importe,
                Descripcion = req.Descripcion ?? string.Empty
            });

            if (req.TipoMovimiento == MovimientoTipo.Debito) totalDebitos += importe;
            else totalCreditos += importe;
        }

        if (Math.Abs(totalDebitos - totalCreditos) > 0.01m)
            throw new InvalidOperationException(
                $"El asiento no se equilibra: débitos {totalDebitos:0.00} vs créditos {totalCreditos:0.00}.");

        var asiento = new AsientoContable
        {
            SucursalId = request.SucursalId,
            UsuarioId = request.UsuarioId,
            FechaUtc = DateTime.UtcNow,
            Tipo = request.Tipo,
            Concepto = request.Concepto ?? string.Empty,
            Notas = request.Concepto ?? string.Empty,
            Detalles = detalles
        };

        db.AsientosContables.Add(asiento);
        await db.SaveChangesAsync(ct);

        foreach (var d in detalles) d.AsientoContableId = asiento.Id;

        var cuentas = await db.CuentasContables.Where(c => detalles.Select(x => x.CuentaId).Contains(c.Id)).ToListAsync(ct);
        foreach (var d in detalles)
        {
            var cuenta = cuentas.First(c => c.Id == d.CuentaId);
            cuenta.SaldoActual = d.TipoMovimiento == MovimientoTipo.Debito
                ? cuenta.SaldoActual + d.Importe
                : cuenta.SaldoActual - d.Importe;
        }

        await db.SaveChangesAsync(ct);

        var detallesDto = detalles.Select(d => new DetalleAsientoDto(
            d.Id, d.CuentaId, d.Cuenta!.Nombre, d.Cuenta.Codigo, d.TipoMovimiento, d.Importe, d.Descripcion)).ToList();

        return new RegistrarAsientoResult(asiento.Id, asiento.Numeracion ?? $\"AS-{asiento.Id}\", detallesDto, totalDebitos, totalCreditos, true);
    }

    public Task<AsientoContableDto?> DetalleAsientoAsync(int asientoId, CancellationToken ct = default)
    {
        return db.AsientosContables
            .Where(a => a.SucursalId == ctx.SucursalId)
            .Where(a => a.Id == asientoId)
            .Select(a => new AsientoContableDto(
                a.Id, a.SucursalId, a.FechaUtc, a.Tipo, a.Numeracion, a.Concepto,
                a.Detalles.Select(d => new DetalleAsientoDto(
                    d.Id, d.CuentaId, d.Cuenta!.Nombre, d.Cuenta.Codigo, d.TipoMovimiento, d.Importe, d.Descripcion)).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<DetalleAsientoDto>> LibroMayorAsync(int sucursalId, DateTime desde, DateTime hasta, int? cuentaId, int pagina, CancellationToken ct = default)
    {
        var q = db.DetallesAsientos.AsNoTracking()
            .Where(d => d.AsientoContable.SucursalId == sucursalId
                && d.AsientoContable.FechaUtc >= DateTime.SpecifyKind(desde, DateTimeKind.Local).ToUniversalTime()
                && d.AsientoContable.FechaUtc < DateTime.SpecifyKind(hasta, DateTimeKind.Local).ToUniversalTime());

        if (cuentaId.HasValue) q = q.Where(d => d.CuentaId == cuentaId.Value);

        int pageSize = 100;
        var total = q.Count();
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        var filas = q.OrderByDescending(d => d.AsientoContable.FechaUtc)
            .ThenBy(d => d.AsientoContable.Numeracion)
            .Skip((pagina - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DetalleAsientoDto(d.Id, d.CuentaId, d.Cuenta!.Nombre, d.Cuenta.Codigo, d.TipoMovimiento, d.Importe, d.Descripcion))
            .ToList();

        return Task.FromResult(filas);
    }
}
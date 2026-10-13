using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Validación + póliza automática del gasto (gasto vs. banco/efectivo).</summary>
public sealed partial class GastoService
{
    private static (string Categoria, string Concepto) Validar(CrearGastoRequest req)
    {
        if (req.Importe <= 0) throw new InvalidOperationException("El importe debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(req.Concepto)) throw new InvalidOperationException("El concepto es obligatorio.");
        var categoria = (req.Categoria ?? "").Trim();
        if (!Enum.TryParse<CategoriaGasto>(categoria, out _))
            throw new InvalidOperationException($"Categoría inválida. Valores: {string.Join(", ", Enum.GetNames<CategoriaGasto>())}.");
        if (!FormasValidas.Contains(req.FormaPagoSat))
            throw new InvalidOperationException("Forma de pago SAT inválida (01, 02, 03, 04, 28, 99).");
        return (categoria, req.Concepto.Trim());
    }

    private static (DateTime Ini, DateTime Fin) Rango(DateTime desde, DateTime hasta)
    {
        if (hasta < desde) throw new InvalidOperationException("El rango de fechas es inválido.");
        var ini = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var fin = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        return (ini, fin);
    }

    private static DateTime NormalizarFecha(DateTime fecha) =>
        fecha.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(fecha, DateTimeKind.Local).ToUniversalTime()
            : fecha.ToUniversalTime();

    /// <summary>
    /// Póliza automática con las ReglasAsiento del evento "Gasto".
    /// Si aún no hay reglas sembradas (BD previa), cae a 6000↔1000/1100.
    /// </summary>
    private async Task RegistrarPolizaAsync(Domain.Entities.Gasto gasto, CancellationToken ct)
    {
        List<RegistrarDetalleAsientoRequest>? lineas = null;
        try
        {
            var reglas = await db.ReglasAsiento.AsNoTracking()
                .Where(r => r.Evento == "Gasto" && r.Activo)
                .OrderBy(r => r.Orden).ToListAsync(ct);
            if (reglas.Count >= 2)
            {
                lineas = [];
                foreach (var r in reglas)
                {
                    var tipo = r.Debe ? MovimientoTipo.Debito : MovimientoTipo.Credito;
                    lineas.Add(new RegistrarDetalleAsientoRequest(r.CuentaId, tipo, gasto.Importe, gasto.Concepto));
                }
            }
        }
        catch { lineas = null; }
        lineas ??= await LineasPorDefectoAsync(gasto, ct);
        if (lineas.Count == 0) return;
        try
        {
            await cuentas.RegistrarAsientoAsync(new RegistrarAsientoRequest(
                gasto.SucursalId, gasto.UsuarioId, "Operativo",
                $"Gasto {gasto.Categoria} #{gasto.Id} {gasto.Concepto}", lineas), ct);
        }
        catch
        {
            // La póliza es informativa: un catálogo sin las cuentas no tumba el gasto.
        }
    }

    private async Task<List<RegistrarDetalleAsientoRequest>> LineasPorDefectoAsync(
        Domain.Entities.Gasto gasto, CancellationToken ct)
    {
        var contraCodigo = gasto.FormaPagoSat == "01" ? "1000" : "1100";
        var codigos = new[] { "6000", contraCodigo };
        var ids = await db.CuentasContables.AsNoTracking()
            .Where(c => c.SucursalId == gasto.SucursalId && codigos.Contains(c.Codigo))
            .ToDictionaryAsync(c => c.Codigo, c => c.Id, ct);
        if (!ids.TryGetValue("6000", out var gastoId)) return [];
        if (!ids.TryGetValue(contraCodigo, out var contraId)) return [];
        return
        [
            new RegistrarDetalleAsientoRequest(gastoId, MovimientoTipo.Debito, gasto.Importe, gasto.Concepto),
            new RegistrarDetalleAsientoRequest(contraId, MovimientoTipo.Credito, gasto.Importe, gasto.Concepto)
        ];
    }
}

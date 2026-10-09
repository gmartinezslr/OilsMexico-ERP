using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Motor de comisiones (fase 3). Ver <see cref="IComisionesService"/>.
/// <para>
/// División de responsabilidades, a propósito:
/// <list type="bullet">
/// <item><see cref="ICobranzaService"/> responde «¿cuánto entró de verdad, de quién y en qué
///       litros?» (cash basis, prorrateo por renglón y base sin IVA).</item>
/// <item>Este servicio responde «¿cuánto se gana con eso?»: aplica metas y el tabulador % por
///       presentación, y congela el resultado del cierre para que sea auditable.</item>
/// </list>
/// Nunca recalcula cobranza por su cuenta: eso duplicaría la regla y la haría divergir.
/// </para>
/// </summary>
public sealed partial class ComisionesService(
    ErpDbContext db, ICobranzaService cobranza, ISucursalContext ctx) : IComisionesService
{
    // =================================================================================
    // Cuotas (metas mensuales)
    // =================================================================================

    public async Task<List<CuotaDto>> ListarCuotasAsync(
        int? vendedorId, int? anio, int? mes, CancellationToken ct = default)
    {
        // La lista de metas es tablero administrativo: un vendedor ve la suya por su portal,
        // no el tablero completo (ahí están los objetivos y los pisos de todos).
        ExigirAdmin("ver las metas de los vendedores");
        var q = db.CuotasVendedor.AsNoTracking().AsQueryable();
        if (vendedorId is not null) q = q.Where(c => c.VendedorId == vendedorId);
        if (anio is not null) q = q.Where(c => c.Anio == anio);
        if (mes is not null) q = q.Where(c => c.Mes == mes);

        var cuotas = await q.OrderBy(c => c.Anio).ThenBy(c => c.Mes).ThenBy(c => c.VendedorId)
            .ToListAsync(ct);

        var nombres = await NombresVendedoresAsync(cuotas.Select(c => (int?)c.VendedorId), ct);
        return cuotas.Select(c => new CuotaDto(
            c.Id, c.VendedorId, Nombre(nombres, c.VendedorId), c.Anio, c.Mes,
            c.MetaDinero, c.MetaLitros, c.Operador, c.AccionIncumplimiento,
            c.MontoPagoMinimo, c.Notas)).ToList();
    }

    public async Task<CuotaDto> GuardarCuotaAsync(GuardarCuotaRequest req, CancellationToken ct = default)
    {
        ExigirAdmin("modificar las metas de comisión");
        ValidarPeriodo(req.Anio, req.Mes);
        if (req.MetaDinero is null && req.MetaLitros is null)
            throw new InvalidOperationException(
                "La cuota debe tener al menos una meta activa (dinero o litros).");
        if (req.MetaDinero < 0 || req.MetaLitros < 0 || req.MontoPagoMinimo < 0)
            throw new InvalidOperationException("Las metas y el pago mínimo no pueden ser negativos.");
        // El piso garantizado sin la acción que lo usa es una trampa silenciosa: se captura
        // «me pagarán X» y el motor lo ignora.
        if (req.MontoPagoMinimo > 0 && req.AccionIncumplimiento != AccionIncumplimiento.PagoMinimo)
            throw new InvalidOperationException(
                "Capturaste un monto mínimo pero la acción de incumplimiento no es 'PagoMinimo': " +
                "el monto se ignoraría. Cambia la acción o deja el monto en 0.");

        var vendedor = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == req.VendedorId, ct)
            ?? throw new InvalidOperationException($"El vendedor {req.VendedorId} no existe.");
        if (!vendedor.Activo)
            throw new InvalidOperationException(
                $"El usuario {vendedor.Nombre} está inactivo; no se le pueden asignar metas.");
        if (!EsRolComisionable(vendedor.Rol))
            throw new InvalidOperationException(
                $"El usuario {vendedor.Nombre} tiene rol '{vendedor.Rol}', que no es un rol con comisiones.");

        // Coherencia del operador: exigir SOLO_DINERO sin meta de dinero (o SOLO_VOLUMEN sin
        // meta de litros) haría que el motor evaluara sobre una meta inexistente. Se valida
        // ANTES de tocar el contexto: si esto lanzara después del Add, la entidad quedaría
        // rastreada como 'Added' y se colaría en el siguiente SaveChanges del mismo scope.
        if (req.Operador is OperadorLogico.SoloDinero or OperadorLogico.Y
            && !(req.MetaDinero > 0))
            throw new InvalidOperationException(
                $"El operador '{req.Operador}' requiere una meta de dinero mayor a 0.");
        if (req.Operador is OperadorLogico.SoloVolumen or OperadorLogico.Y
            && !(req.MetaLitros > 0))
            throw new InvalidOperationException(
                $"El operador '{req.Operador}' requiere una meta de litros mayor a 0.");

        // Upsert por (vendedor, periodo): el UNIQUE ya impide duplicar, así que se decide
        // actualizar la existente en vez de reventar con un error de constraint opaco.
        var existente = await db.CuotasVendedor.FirstOrDefaultAsync(
            c => c.VendedorId == req.VendedorId && c.Anio == req.Anio && c.Mes == req.Mes, ct);

        if (existente is null)
        {
            existente = new CuotaVendedor { VendedorId = req.VendedorId, Anio = req.Anio, Mes = req.Mes };
            db.CuotasVendedor.Add(existente);
        }

        existente.MetaDinero = req.MetaDinero;
        existente.MetaLitros = req.MetaLitros;
        existente.Operador = req.Operador;
        existente.AccionIncumplimiento = req.AccionIncumplimiento;
        existente.MontoPagoMinimo = req.AccionIncumplimiento == AccionIncumplimiento.PagoMinimo
            ? req.MontoPagoMinimo : 0m;

        existente.Notas = req.Notas;
        existente.ActualizadoUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new CuotaDto(existente.Id, existente.VendedorId, vendedor.Nombre, existente.Anio,
            existente.Mes, existente.MetaDinero, existente.MetaLitros, existente.Operador,
            existente.AccionIncumplimiento, existente.MontoPagoMinimo, existente.Notas);
    }

    public async Task EliminarCuotaAsync(int cuotaId, CancellationToken ct = default)
    {
        ExigirAdmin("eliminar metas de comisión");
        var cuota = await db.CuotasVendedor.FirstOrDefaultAsync(c => c.Id == cuotaId, ct)
            ?? throw new InvalidOperationException($"La cuota {cuotaId} no existe.");

        // Si el periodo ya está cerrado, borrar la meta dejaría un cierre congelado cuya
        // configuración ya no se puede reconstruir. Se bloquea en vez de dejar basura.
        var cerrado = await db.ComisionesHistorial.AsNoTracking().AnyAsync(
            c => c.VendedorId == cuota.VendedorId && c.Anio == cuota.Anio && c.Mes == cuota.Mes
                && c.Estado != EstadoComision.Borrador, ct);
        if (cerrado)
            throw new InvalidOperationException(
                $"El periodo {cuota.Anio}-{cuota.Mes:D2} ya está cerrado o pagado. " +
                "Reabre el cierre antes de eliminar la meta.");

        db.CuotasVendedor.Remove(cuota);
        await db.SaveChangesAsync(ct);
    }

    // =================================================================================
    // Helpers compartidos
    // =================================================================================

    /// <summary>Roles que cobran comisión. Conta/Almacen operan el sistema, no venden.</summary>
    private static bool EsRolComisionable(string? rol) => rol is "Vendedor" or "Admin";

    /// <summary>Rango [inicio, fin) del periodo en fechas locales, igual que GestionService.</summary>
    private static (DateTime Desde, DateTime Hasta) RangoPeriodo(int anio, int mes)
    {
        var inicio = new DateTime(anio, mes, 1);
        return (inicio, inicio.AddMonths(1));
    }

    private static void ValidarPeriodo(int anio, int mes)
    {
        if (mes is < 1 or > 12) throw new InvalidOperationException("El mes debe estar entre 1 y 12.");
        if (anio is < 2000 or > 2100) throw new InvalidOperationException("El año está fuera de rango.");
    }

    private async Task<Dictionary<int, string>> NombresVendedoresAsync(
        IEnumerable<int?> ids, CancellationToken ct)
    {
        var distintos = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        if (distintos.Count == 0) return new Dictionary<int, string>();
        return await db.Usuarios.AsNoTracking().Where(u => distintos.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
    }

    private static string Nombre(Dictionary<int, string> nombres, int? id) =>
        id is int i && nombres.TryGetValue(i, out var n) ? n : "(sin atribución)";
}


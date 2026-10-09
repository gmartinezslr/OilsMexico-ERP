using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Cierre mensual congelado (comisiones_historial). Ver <see cref="IComisionesService"/>.</summary>
public sealed partial class ComisionesService
{
    /// <summary>
    /// Calcula el periodo y lo CONGELA. Es una fotografía: guarda la cobranza real, las metas
    /// aplicadas y el desglose por producto, así que editar después la cuota o el tabulador no
    /// altera lo ya cerrado (los cierres pasados siguen siendo auditables).
    /// </summary>
    public async Task<ComisionHistorialDto> CerrarPeriodoAsync(
        CerrarPeriodoRequest req, CancellationToken ct = default)
    {
        ExigirAdmin("cerrar el periodo de comisiones");
        ValidarPeriodo(req.Anio, req.Mes);

        var vendedor = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == req.VendedorId, ct)
            ?? throw new InvalidOperationException($"El vendedor {req.VendedorId} no existe.");

        // Un periodo Pagado es inamovible: el dinero ya salió. Reabrirlo falsificaría un pago.
        var pagado = await db.ComisionesHistorial.AsNoTracking().AnyAsync(
            c => c.VendedorId == req.VendedorId && c.Anio == req.Anio && c.Mes == req.Mes
                && c.Estado == EstadoComision.Pagado, ct);
        if (pagado)
            throw new InvalidOperationException(
                $"El periodo {req.Anio}-{req.Mes:D2} ya está PAGADO. No se puede recalcular; " +
                "si hubo un error hay que corregirlo con un ajuste contable, no reescribiendo el historial.");

        var (r, _) = await CalcularInternoAsync(req.VendedorId, req.Anio, req.Mes, null, ct);

        // Upsert por (vendedor, periodo): recalcular reutiliza el mismo renglón y así el
        // historial no se llena de duplicados si se cierra dos veces.
        var hist = await db.ComisionesHistorial.FirstOrDefaultAsync(
            c => c.VendedorId == req.VendedorId && c.Anio == req.Anio && c.Mes == req.Mes, ct);

        if (hist is null)
        {
            hist = new ComisionHistorial
            {
                VendedorId = req.VendedorId,
                Anio = req.Anio,
                Mes = req.Mes,
                CalculadoUtc = DateTime.UtcNow
            };
            db.ComisionesHistorial.Add(hist);
        }

        hist.Estado = EstadoComision.Cerrado;
        hist.DineroReal = r.Cobranza.NetoCobradoSinIva;
        hist.LitrosReales = r.Cobranza.LitrosCobrados;
        hist.CobradoBruto = r.Cobranza.CobradoBrutoConIva;
        hist.FacturasCobradas = r.Cobranza.FacturasCobradas;

        // Se copian las metas aplicadas: es lo que hace al cierre una FOTO y no un cálculo.
        hist.MetaDinero = r.Cuota?.MetaDinero;
        hist.MetaLitros = r.Cuota?.MetaLitros;
        hist.Operador = r.Cuota?.Operador ?? OperadorLogico.SoloDinero;
        hist.AccionIncumplimiento = r.Cuota?.AccionIncumplimiento ?? AccionIncumplimiento.CeroComision;
        hist.MontoPagoMinimo = r.Cuota?.MontoPagoMinimo ?? 0m;

        hist.PctDinero = r.PctDinero;
        hist.PctLitros = r.PctLitros;
        hist.CumplioMeta = r.CumplioMeta;
        hist.DetalleEvaluacion = r.DetalleEvaluacion;

        hist.ComisionBase = r.ComisionBase;
        hist.ComisionBono = r.ComisionBono;
        hist.ComisionFinal = r.ComisionFinal;
        hist.AplicoPagoMinimo = r.AplicoPagoMinimo;
        hist.ProductosSinTabulador = r.SinTabulador.Count > 0
            ? string.Join(", ", r.SinTabulador)
            : null;
        hist.DesgloseJson = SerializarDesglose(r.Productos);

        hist.CalculadoUtc = DateTime.UtcNow;
        hist.CerradoUtc = DateTime.UtcNow;
        // Quién congela el periodo: sin esto, un cierre que se pueda reescribir no demuestra
        // quién autorizó el pago.
        hist.CerradoPorUsuarioId = UsuarioSesion;
        if (req.Notas is not null) hist.Notas = req.Notas;

        await db.SaveChangesAsync(ct);
        return AHistorialDto(hist, vendedor.Nombre);
    }

    public async Task<List<ComisionHistorialDto>> ListarHistorialAsync(
        int? vendedorId, int? anio, CancellationToken ct = default)
    {
        // Un vendedor puede ver SU historial (es su comprobante de pago), pero no el de los
        // demás. Admin ve todo. Se filtra por sesión en vez de confiar en el parámetro: así un
        // id ajeno en la petición no revela nada.
        var q = db.ComisionesHistorial.AsNoTracking().AsQueryable();
        if (EsAdmin)
        {
            if (vendedorId is not null) q = q.Where(h => h.VendedorId == vendedorId);
        }
        else
        {
            var yo = UsuarioSesion
                ?? throw new InvalidOperationException(
                    "No hay sesión activa: no se puede consultar el historial de comisiones.");
            q = q.Where(h => h.VendedorId == yo);
        }
        if (anio is not null) q = q.Where(h => h.Anio == anio);

        var filas = await q.OrderByDescending(h => h.Anio).ThenByDescending(h => h.Mes)
            .ThenBy(h => h.VendedorId).ToListAsync(ct);

        var nombres = await NombresVendedoresAsync(filas.Select(h => (int?)h.VendedorId), ct);
        return filas.Select(h => AHistorialDto(h, Nombre(nombres, h.VendedorId))).ToList();
    }

    /// <summary>
    /// Vuelve el cierre a Borrador para poder recalcularlo. Deja rastro de quién y por qué:
    /// un historial que se puede reescribir sin huella no es historial.
    /// </summary>
    public async Task ReabrirPeriodoAsync(int historialId, string motivo, CancellationToken ct = default)
    {
        ExigirAdmin("reabrir un periodo cerrado");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Para reabrir un periodo cerrado es obligatorio indicar el motivo (queda en el historial).");

        var hist = await db.ComisionesHistorial.FirstOrDefaultAsync(h => h.Id == historialId, ct)
            ?? throw new InvalidOperationException($"El cierre {historialId} no existe.");
        if (hist.Estado == EstadoComision.Pagado)
            throw new InvalidOperationException(
                "El periodo ya está PAGADO: no se puede reabrir.");
        if (hist.Estado == EstadoComision.Borrador)
            return; // idempotente

        hist.Estado = EstadoComision.Borrador;
        hist.Notas = $"REABIERTO ({DateTime.UtcNow:u}) por usuario {UsuarioSesion}: {motivo}" +
                     (string.IsNullOrWhiteSpace(hist.Notas) ? "" : $" | previo: {hist.Notas}");
        await db.SaveChangesAsync(ct);
    }

    public async Task MarcarPagadoAsync(int historialId, CancellationToken ct = default)
    {
        ExigirAdmin("marcar una comisión como pagada");
        var hist = await db.ComisionesHistorial.FirstOrDefaultAsync(h => h.Id == historialId, ct)
            ?? throw new InvalidOperationException($"El cierre {historialId} no existe.");
        if (hist.Estado == EstadoComision.Pagado)
            return; // idempotente
        if (hist.Estado != EstadoComision.Cerrado)
            throw new InvalidOperationException(
                "Sólo se puede marcar como pagado un periodo que esté CERRADO.");

        hist.Estado = EstadoComision.Pagado;
        hist.PagadoUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // =================================================================================
    // Serialización del desglose congelado
    // =================================================================================

    /// <summary>
    /// El desglose se guarda como JSON en vez de tabla hija: es una foto de solo lectura y no
    /// tiene por qué participar en queries ni reportes. Si mañana se necesita agregar, se reabre
    /// el periodo y se regenera.
    /// </summary>
    private static string SerializarDesglose(List<ComisionProductoDto> productos) =>
        System.Text.Json.JsonSerializer.Serialize(productos.Select(p => new
        {
            p.ProductoId,
            p.Producto,
            p.Unidad,
            netoSinIva = p.NetoSinIva,
            litros = p.Litros,
            p.PorcBase,
            p.PorcBono,
            comisionBase = p.ComisionBase,
            comisionBono = p.ComisionBono
        }));

    private static List<string> LeerSinTabulador(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private static ComisionHistorialDto AHistorialDto(ComisionHistorial h, string vendedor) =>
        new(h.Id, h.VendedorId, vendedor, h.Anio, h.Mes, h.Estado,
            h.DineroReal, h.LitrosReales, h.CobradoBruto, h.FacturasCobradas,
            h.MetaDinero, h.MetaLitros, h.Operador, h.AccionIncumplimiento, h.MontoPagoMinimo,
            h.PctDinero, h.PctLitros, h.CumplioMeta, h.DetalleEvaluacion,
            h.ComisionBase, h.ComisionBono, h.ComisionFinal, h.AplicoPagoMinimo,
            LeerSinTabulador(h.ProductosSinTabulador),
            h.CalculadoUtc, h.CerradoUtc, h.PagadoUtc, h.Notas);
}

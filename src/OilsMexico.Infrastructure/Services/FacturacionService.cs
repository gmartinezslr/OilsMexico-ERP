using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Facturación CFDI 4.0: listado por sucursal, timbrado diferido (sellado + PAC)
/// y cancelación real ante Finkok (cancel_signature con CSD local).
/// </summary>
public sealed class FacturacionService(
    ErpDbContext db, ISucursalContext ctx,
    ICfdiSelladoService sellado, IPacTimbradoService pac) : IFacturacionService
{
    public async Task<List<FacturaListadoDto>> ListarAsync(
        int sucursalId, EstadoFactura? estado, string? texto, CancellationToken ct = default)
    {
        // REGLA #1: solo Admin puede consultar otra sucursal.
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var q = db.Facturas.AsNoTracking().Where(f => f.SucursalId == suc);

        if (estado is not null)
            q = q.Where(f => f.Estado == estado);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(f => f.FolioInterno.ToLower().Contains(t)
                || (f.Cliente != null && f.Cliente.Nombre.ToLower().Contains(t)));
        }

        var filas = await q.OrderByDescending(f => f.FechaEmision).Take(200)
            .Select(f => new
            {
                f.Id, f.FolioInterno, f.FechaEmision, f.Estado,
                ClienteNombre = f.Cliente != null ? f.Cliente.Nombre : null,
                f.Subtotal, f.Iva, f.Total, f.FormaPagoSat, f.MetodoPagoSat, f.UsoCfdi,
                f.UuidSat, TieneXml = f.XmlSellado != null,
                f.MotivoCancelacion, Renglones = f.Detalles.Count
            })
            .ToListAsync(ct);

        // El enum viaja tipado al DTO; la conversión a texto (para UI) ocurre al renderizar.
        return filas.Select(f => new FacturaListadoDto(
            f.Id, f.FolioInterno, f.FechaEmision,
            f.ClienteNombre ?? "(sin cliente)", f.Estado,
            f.Subtotal, f.Iva, f.Total,
            f.FormaPagoSat, f.MetodoPagoSat, f.UsoCfdi,
            f.UuidSat, f.TieneXml, f.Renglones, f.MotivoCancelacion)).ToList();
    }

    public async Task<VentaPosResult> TimbrarAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas.FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes timbrar facturas de otra sucursal.");
        if (f.Estado != EstadoFactura.Pendiente)
            throw new InvalidOperationException(
                $"Solo se timbran facturas pendientes (estado actual: {f.Estado}).");
        if (f.UuidSat is not null)
            throw new InvalidOperationException("La factura ya tiene UUID timbrado.");

        // Mismo flujo que el POS (VentasService.RegistrarVentaAsync):
        // sellado RSA-SHA256 (CSD real o sello DEV simulado) + timbrado vía PAC (Finkok o simulado).
        await sellado.SellarAsync(facturaId, ct);
        var (uuidTimbrado, xmlTimbrado) = await pac.TimbrarAsync(f.XmlSellado!, ct);
        f.UuidSat = uuidTimbrado;
        f.XmlSellado = xmlTimbrado; // XML con el Timbre Fiscal Digital del PAC
        f.Estado = EstadoFactura.Timbrada;
        await db.SaveChangesAsync(ct);

        return new VentaPosResult(f.Id, f.FolioInterno, f.UuidSat,
            f.Subtotal, f.Iva, f.Total, "TIMBRADA");
    }

    public async Task<VentaPosResult> CancelarAsync(int facturaId, string motivo, string? folioSustitucion = null, CancellationToken ct = default)
    {
        var f = await db.Facturas.FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cancelar facturas de otra sucursal.");
        if (f.Estado is not (EstadoFactura.Timbrada or EstadoFactura.Entregada))
            throw new InvalidOperationException(
                $"Solo se cancelan facturas timbradas (estado actual: {f.Estado}). Para anular una venta sin timbrar usa Devolución en el Historial.");
        var motivoSat = (motivo ?? string.Empty).Trim();
        if (motivoSat is not ("01" or "02" or "03" or "04"))
            throw new InvalidOperationException(
                $"Motivo de cancelación SAT inválido: '{motivoSat}'. Usa 01 (sustitución con relación), 02, 03 o 04.");
        if (f.UuidSat is null)
            throw new InvalidOperationException("La factura no tiene UUID timbrado; no hay nada que cancelar ante el PAC.");

        // Cancelación real ante Finkok (cancel_signature con CSD local). En SIMULADO no toca red.
        await pac.CancelarAsync(f.UuidSat.Value, motivoSat,
            motivoSat == "01" ? folioSustitucion?.Trim() : null, ct);

        f.Estado = EstadoFactura.Cancelada;
        f.MotivoCancelacion = motivoSat == "01" && !string.IsNullOrWhiteSpace(folioSustitucion)
            ? $"{motivoSat} (sustituye: {folioSustitucion.Trim()})"
            : motivoSat;
        await db.SaveChangesAsync(ct);
        return new VentaPosResult(f.Id, f.FolioInterno, f.UuidSat,
            f.Subtotal, f.Iva, f.Total, "CANCELADA");
    }

    public async Task<string?> ObtenerXmlAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct);
        if (f is null) return null;
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId) return null; // aislamiento
        return f.XmlSellado;
    }
}
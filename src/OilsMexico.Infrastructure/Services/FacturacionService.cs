using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Facturación CFDI 4.0: listado por sucursal, timbrado diferido (sellado + PAC)
/// y cancelación (solo modo SIMULADO hasta integrar el WS real del PAC).
/// </summary>
public sealed class FacturacionService(
    ErpDbContext db, ISucursalContext ctx,
    ICfdiSelladoService sellado, IPacTimbradoService pac,
    IConfiguration cfg) : IFacturacionService
{
    public async Task<List<FacturaListadoDto>> ListarAsync(
        int sucursalId, string? estado, string? texto, CancellationToken ct = default)
    {
        // REGLA #1: solo Admin puede consultar otra sucursal.
        var suc = ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId;
        var q = db.Facturas.AsNoTracking().Where(f => f.SucursalId == suc);

        if (!string.IsNullOrWhiteSpace(estado)
            && Enum.TryParse<EstadoFactura>(estado, ignoreCase: true, out var est))
            q = q.Where(f => f.Estado == est);

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

        // ToString() del enum en cliente (no traducible a SQL).
        return filas.Select(f => new FacturaListadoDto(
            f.Id, f.FolioInterno, f.FechaEmision,
            f.ClienteNombre ?? "(sin cliente)", f.Estado.ToString(),
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
        // sellado RSA-SHA256 (modo SIMULADO sin CSD) + timbrado vía PAC.
        await sellado.SellarAsync(facturaId, ct);
        f.UuidSat = await pac.TimbrarAsync(f.XmlSellado!, ct);
        f.Estado = EstadoFactura.Timbrada;
        await db.SaveChangesAsync(ct);

        return new VentaPosResult(f.Id, f.FolioInterno, f.UuidSat,
            f.Subtotal, f.Iva, f.Total, "TIMBRADA");
    }

    public async Task<VentaPosResult> CancelarAsync(int facturaId, string motivo, CancellationToken ct = default)
    {
        var f = await db.Facturas.FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cancelar facturas de otra sucursal.");
        if (f.Estado is not (EstadoFactura.Timbrada or EstadoFactura.Entregada))
            throw new InvalidOperationException(
                $"Solo se cancelan facturas timbradas (estado actual: {f.Estado}). Para anular una venta sin timbrar usa Devolución en el Historial.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("El motivo de cancelación es obligatorio (lo exige el SAT).");
        if (!string.Equals(cfg["Cfdi:PacModo"] ?? "SIMULADO", "SIMULADO", StringComparison.OrdinalIgnoreCase))
            throw new NotImplementedException(
                "La cancelación contra el PAC real aún no está habilitada. Configura el WS de cancelación o usa Cfdi:PacModo=SIMULADO.");

        f.Estado = EstadoFactura.Cancelada;
        f.MotivoCancelacion = motivo.Trim();
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
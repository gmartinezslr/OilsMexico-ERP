using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class ComplementoPagoService
{
    public async Task<CobroResult> RegistrarCobroAsync(RegistrarCobroRequest req, CancellationToken ct = default)
    {
        if (req.Monto <= 0) throw new InvalidOperationException("El monto debe ser mayor a cero.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var f = await db.Facturas.FirstOrDefaultAsync(x => x.Id == req.FacturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");
        if (ctx.Rol != "Admin" && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cobrar facturas de otra sucursal.");
        if (f.MetodoPagoSat != "PPD")
            throw new InvalidOperationException("Solo las ventas PPD se liquidan con REP.");
        if (f.UuidSat is null)
            throw new InvalidOperationException("Timbra la factura PPD antes de cobrarla (el REP referencia su UUID).");
        var cobrado = await db.VentaCobros
            .Where(c => c.FacturaId == f.Id).SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;
        var saldoAnt = Math.Round(f.Total - cobrado, 2);
        if (saldoAnt <= 0.01m) throw new InvalidOperationException("La factura ya está liquidada.");
        if (req.Monto - saldoAnt > 0.01m)
            throw new InvalidOperationException($"El monto excede el saldo (${saldoAnt:N2}).");

        var numParc = await db.VentaCobros.CountAsync(c => c.FacturaId == f.Id, ct) + 1;
        var monto = Math.Round(req.Monto, 2);
        var insoluto = Math.Round(saldoAnt - monto, 2);
        var rep = new ComplementoPago
        {
            SucursalId = f.SucursalId,
            FolioInterno = $"REP-{f.SucursalId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ClienteId = f.ClienteId, FechaEmision = DateTime.UtcNow,
            Total = monto, Estado = "Pendiente", UsuarioId = ctx.UsuarioId
        };
        db.ComplementosPago.Add(rep);
        await db.SaveChangesAsync(ct);

        var cobro = new VentaCobro
        {
            SucursalId = f.SucursalId, FacturaId = f.Id, ClienteId = f.ClienteId,
            Monto = monto, FormaPagoSat = req.FormaPagoSat,
            FechaPagoUtc = DateTime.UtcNow, Referencia = req.Referencia?.Trim(),
            UsuarioId = ctx.UsuarioId, ComplementoPagoId = rep.Id
        };
        db.VentaCobros.Add(cobro);
        await db.SaveChangesAsync(ct);

        rep.Documentos.Add(new ComplementoPagoDetalle
        {
            FacturaId = f.Id, VentaCobroId = cobro.Id,
            UuidFactura = f.UuidSat, FolioFactura = f.FolioInterno,
            NumParcialidad = numParc, ImpSaldoAnt = saldoAnt,
            ImpPagado = monto, ImpSaldoInsoluto = insoluto, Moneda = "MXN"
        });
        await db.SaveChangesAsync(ct);

        // Sellado real Tipo P + Pagos 2.0 (CSD + XSLT SAT) + timbrado PAC.
        await sellado.SellarRepAsync(rep.Id, ct);
        var (uuidRep, xmlRepTimbrado) = await pac.TimbrarAsync(rep.XmlSellado!, ct);
        rep.UuidSat = uuidRep;
        rep.XmlSellado = xmlRepTimbrado;
        rep.Estado = "Timbrado";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new CobroResult(cobro.Id, rep.Id, rep.FolioInterno, monto, insoluto, "TIMBRADO");
    }
}

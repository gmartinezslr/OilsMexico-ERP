using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class VentasService
{
    public async Task<VentaPosResult> RegistrarVentaAsync(VentaPosRequest req, CancellationToken ct = default)
    {
        var sucursalId = ctx.SucursalId;
        if (req.Items.Count == 0) throw new InvalidOperationException("Carrito vacío.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cliente = await db.Clientes.FindAsync([req.ClienteId], ct)
            ?? throw new InvalidOperationException("Cliente no existe.");
        var factura = new Factura
        {
            SucursalId = sucursalId,
            FolioInterno = $"V-{sucursalId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ClienteId = cliente.Id, FormaPagoSat = req.FormaPagoSat,
            MetodoPagoSat = req.MetodoPagoSat, UsoCfdi = req.UsoCfdi,
            Estado = EstadoFactura.Pendiente
        };
        decimal subtotal = 0m;
        foreach (var item in req.Items)
        {
            var unidad = await db.UnidadesMedida.FindAsync([item.UnidadMedidaId], ct);
            var factor = unidad?.FactorConversion ?? item.FactorConversion;
            var litros = item.Cantidad * factor;
            var existencias = await db.InventarioSucursal
                .Include(i => i.Lote)
                .Where(i => i.SucursalId == sucursalId && i.ProductoId == item.ProductoId && i.StockActual > 0)
                .OrderBy(i => i.Lote != null ? i.Lote.FechaCaducidad : DateOnly.MaxValue)
                .ToListAsync(ct);
            if (existencias.Sum(i => i.StockActual) < litros)
                throw new InvalidOperationException($"Stock insuficiente para {item.Nombre}.");
            var restante = litros;
            int? loteSurtido = null;
            foreach (var ex in existencias)
            {
                if (restante <= 0) break;
                var toma = Math.Min(ex.StockActual, restante);
                ex.StockActual -= toma;
                ex.ActualizadoUtc = DateTime.UtcNow;
                restante -= toma;
                loteSurtido ??= ex.LoteId;
                db.Movimientos.Add(new MovimientoInventario
                {
                    SucursalId = sucursalId, ProductoId = item.ProductoId,
                    LoteId = ex.LoteId, CantidadLitros = -toma,
                    Tipo = "VENTA", UsuarioId = ctx.UsuarioId
                });
                if (ex.Lote is not null) ex.Lote.CantidadDisponible -= toma;
            }
            var prod = await db.Productos.FindAsync([item.ProductoId], ct);
            var precio = cliente.TipoPrecio == "mayoreo"
                ? (prod?.PrecioMayoreo ?? item.PrecioUnitario) : item.PrecioUnitario;
            var importe = item.Cantidad * precio;
            subtotal += importe;
            factura.Detalles.Add(new FacturaDetalle
            {
                ProductoId = item.ProductoId, UnidadMedidaId = item.UnidadMedidaId,
                UnidadNombre = item.UnidadNombre, Cantidad = item.Cantidad,
                LitrosDescontados = litros, PrecioUnitario = precio,
                Importe = importe, LoteId = loteSurtido
            });
        }
        factura.Subtotal = Math.Round(subtotal / (1 + TasaIva), 2);
        factura.Iva = Math.Round(subtotal - factura.Subtotal, 2);
        factura.Total = Math.Round(subtotal, 2);
        db.Facturas.Add(factura);
        await db.SaveChangesAsync(ct);
        foreach (var m in db.ChangeTracker.Entries<MovimientoInventario>()
                     .Where(e => e.Entity.ReferenciaId == null && e.Entity.Tipo == "VENTA"))
            m.Entity.ReferenciaId = factura.Id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        Guid? uuid = null;
        if (req.RequiereFactura)
        {
            await sellado.SellarAsync(factura.Id, ct);
            var (uuidTimbrado, xmlTimbrado) = await pac.TimbrarAsync(factura.XmlSellado!, ct);
            uuid = uuidTimbrado;
            factura.UuidSat = uuidTimbrado;
            factura.XmlSellado = xmlTimbrado; // XML con el Timbre Fiscal Digital del PAC
            factura.Estado = EstadoFactura.Timbrada;
            await db.SaveChangesAsync(ct);
        }
        return new VentaPosResult(factura.Id, factura.FolioInterno, uuid,
            factura.Subtotal, factura.Iva, factura.Total,
            factura.Estado.ToString().ToUpperInvariant());
    }
}

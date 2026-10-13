using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CotizacionesService
{
    /// <summary>
    /// Factura una orden de venta Confirmada (Capa B): genera la venta/CFDI con los precios de
    /// compromiso congelados en la orden y liga la factura resultante (estado Facturada).
    /// Reutiliza <see cref="IVentasService.RegistrarVentaAsync"/> para no duplicar la lógica de
    /// descuento de stock FIFO, cobro PUE y timbrado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IVA: la orden guarda precios SIN IVA (subtotal = Σ importes + IVA aparte) mientras que el
    /// módulo de ventas/facturación trabaja precios CON IVA incluido (factura.Subtotal =
    /// BaseDeTotal del Σ importes). Por eso cada renglón se lleva a precio con IVA
    /// (× 1.16 vía <see cref="Impuestos"/>), preservando el total comprometido.
    /// </para>
    /// <para>
    /// La venta se registra con <c>CongelarPrecios = true</c>: el precio mayoreo del cliente NO
    /// sustituye al precio comprometido en la orden. La sesión debe estar en la sucursal de la
    /// orden, porque el descuento de stock y la factura se registran en la sucursal de sesión.
    /// </para>
    /// </remarks>
    public async Task<OrdenVentaFacturarResult> ConvertirOrdenAVentaAsync(int ordenId, OrdenVentaFacturarRequest req, CancellationToken ct = default)
    {
        var orden = await db.OrdenesVenta
            .Include(o => o.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(o => o.Id == ordenId, ct)
            ?? throw new InvalidOperationException("La orden de venta no existe.");

        if (orden.Estado != EstadoOrdenVenta.Confirmada)
            throw new InvalidOperationException("Solo las órdenes en estado Confirmada se pueden facturar.");
        if (orden.FacturaId.HasValue)
            throw new InvalidOperationException("La orden ya fue facturada.");
        if (orden.SucursalId != ctx.SucursalId)
            throw new InvalidOperationException("La orden pertenece a otra sucursal; cambia a esa sucursal para facturarla.");
        if (orden.Detalles.Count == 0)
            throw new InvalidOperationException("La orden no tiene renglones para facturar.");

        // Códigos SAT: default (origen único CfdiDefaults) cuando la UI no manda valor.
        var formaPago = string.IsNullOrWhiteSpace(req.FormaPagoSat) ? CfdiDefaults.FormaPagoVenta : req.FormaPagoSat.Trim();
        var metodoPago = string.IsNullOrWhiteSpace(req.MetodoPagoSat) ? CfdiDefaults.MetodoPago : req.MetodoPagoSat.Trim();
        var usoCfdi = string.IsNullOrWhiteSpace(req.UsoCfdi) ? CfdiDefaults.UsoCfdiVenta : req.UsoCfdi.Trim();

        // Factores de conversión de las unidades de la orden (el POS los usa para descontar litros).
        var unidadIds = orden.Detalles.Select(d => d.UnidadMedidaId).Distinct().ToArray();
        var factores = await db.UnidadesMedida.AsNoTracking()
            .Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FactorConversion, ct);

        var items = new List<CarritoItemDto>(orden.Detalles.Count);
        foreach (var d in orden.Detalles.OrderBy(x => x.Id))
        {
            if (d.Cantidad <= 0)
                throw new InvalidOperationException("La orden tiene renglones con cantidad inválida.");
            if (d.Importe <= 0)
                throw new InvalidOperationException($"El renglón del producto {d.ProductoId} no tiene importe válido.");

            // Importe = precio de compromiso YA con descuento (base sin IVA); el módulo de ventas
            // espera precio CON IVA incluido, así que el renglón se lleva a precio con IVA.
            var precioConIva = Math.Round(d.Importe * (1m + Impuestos.TasaIva) / d.Cantidad, 4);
            items.Add(new CarritoItemDto(
                d.ProductoId, d.Producto?.Sku ?? string.Empty, d.Producto?.Nombre ?? string.Empty,
                d.UnidadMedidaId, d.UnidadNombre, factores.GetValueOrDefault(d.UnidadMedidaId, 1m),
                d.Cantidad, precioConIva));
        }

        var venta = await ventas.RegistrarVentaAsync(new VentaPosRequest(
            orden.SucursalId, orden.ClienteId, ctx.UsuarioId, orden.VendedorId,
            formaPago, metodoPago, usoCfdi, req.RequiereFactura, items,
            CongelarPrecios: true), ct);

        orden.FacturaId = venta.FacturaId;
        orden.Estado = EstadoOrdenVenta.Facturada;
        orden.Notas = orden.Notas + (orden.Notas is null or "" ? "" : Environment.NewLine) + $"Facturada: {venta.FolioInterno}";
        await db.SaveChangesAsync(ct);

        return new OrdenVentaFacturarResult(
            orden.Id, orden.FolioInterno ?? string.Empty, orden.Estado,
            venta.FacturaId, venta.FolioInterno, venta.Estado,
            venta.Subtotal, venta.Iva, venta.Total, venta.UuidSat);
    }
}
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Cotizaciones y órdenes de venta. REGLA INMUTABLE #1: todo se filtra por <see cref="ISucursalContext"/>.</summary>
public sealed partial class CotizacionesService(ErpDbContext db, ISucursalContext ctx, IVentasPosService ventas) : ICotizacionesService
{
    private static decimal CalcularImporte(decimal cantidad, decimal precioUnitario, decimal descuento) =>
        Math.Round((cantidad * precioUnitario) - descuento, 2);

    private static decimal CalcularTotal(decimal subtotal) =>
        Math.Round(subtotal + Impuestos.IvaDeBase(subtotal), 2);

    /// <summary>Asigna el folio interno siguiente (norma: CT-{suc}-{yyyyMMddHHmmss}).</summary>
    private static string AsignarFolioInterno(int sucursalId, DateTime now) =>
        $"CT-{sucursalId}-{now:yyyyMMddHHmmss}";

    /// <summary>Asigna el folio interno siguiente (norma: OV-{suc}-{yyyyMMddHHmmss}).</summary>
    private static string AsignarFolioOrdenVenta(int sucursalId, DateTime now) =>
        $"OV-{sucursalId}-{now:yyyyMMddHHmmss}";

    /// <summary>Totales calculados a partir de los renglones (sin IVA para el listado).</summary>
    private static (decimal Subtotal, decimal Iva, decimal Total) CalcularTotales(List<CotizacionDetalle> detalles)
    {
        decimal subtotal = detalles.Sum(d => CalcularImporte(d.Cantidad, d.PrecioUnitario, d.Descuento));
        return (Math.Round(subtotal, 2), Impuestos.IvaDeBase(subtotal), CalcularTotal(subtotal));
    }

    /// <summary>Totales calculados a partir de los renglones de una orden de venta.</summary>
    private static (decimal Subtotal, decimal Iva, decimal Total) CalcularTotalesOrden(List<OrdenVentaDetalle> detalles)
    {
        decimal subtotal = detalles.Sum(d => CalcularImporte(d.Cantidad, d.PrecioUnitario, d.Descuento));
        return (Math.Round(subtotal, 2), Impuestos.IvaDeBase(subtotal), CalcularTotal(subtotal));
    }

    /// <summary>Clonado de los renglones de una cotización a renglones de orden de venta (congela el precio).</summary>
    private static List<OrdenVentaDetalle> ClonarDetalles(List<CotizacionDetalle> fuente) =>
        fuente.Select(d => new OrdenVentaDetalle
        {
            ProductoId = d.ProductoId, UnidadMedidaId = d.UnidadMedidaId,
            UnidadNombre = d.UnidadNombre, Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario, Descuento = d.Descuento, LoteId = d.LoteId,
            Importe = Math.Round((d.Cantidad * d.PrecioUnitario) - d.Descuento, 2)
        }).ToList();

    /// <summary>Marca como Expirada las cotizaciones vencidas (solo no convertidas).</summary>
    public async Task<List<CotizacionListadoDto>> ExpirarCotizacionesAsync(int sucursalId, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var desactuales = await db.Cotizaciones
            .Where(c => c.SucursalId == sucursalId
                && c.Estado == EstadoCotizacion.Borrador
                && c.FechaVencimiento != null
                && c.FechaVencimiento <= ahora)
            .ToListAsync(ct);

        foreach (var c in desactuales)
        {
            c.Estado = EstadoCotizacion.Expirada;
            c.FechaVencimiento = ahora;
        }

        if (desactuales.Count > 0)
            await db.SaveChangesAsync(ct);

        return desactuales.Select(MapearACotizacionListadoDto).ToList();
    }
}

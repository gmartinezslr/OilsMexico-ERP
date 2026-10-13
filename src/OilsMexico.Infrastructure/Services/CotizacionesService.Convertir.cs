using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CotizacionesService
{
    /// <summary>
    /// Convierte una cotización (solo estado Borrador) en una orden de venta.
    /// Los precios se congelan como precio de compromiso y el vendedor se asigna
    /// según lo indicado; si no se indica, se usa el dueño comercial del cliente.
    /// </summary>
    public async Task<CotizacionConvertirResult> ConvertirCotizacionAsync(int id, CotizacionConvertirRequest req, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones
            .Include(c => c.Cliente)
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new InvalidOperationException("La cotización no existe.");

        if (cotizacion.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("Solo las cotizaciones en estado Borrador se pueden convertir.");

        int? vendedorId = req.VendedorId;
        if (vendedorId is not null)
        {
            var vendedor = await db.Usuarios.FindAsync([vendedorId.Value], ct)
                ?? throw new InvalidOperationException("El vendedor indicado no existe.");
            if (!vendedor.Activo) throw new InvalidOperationException("El vendedor indicado está inactivo.");
        }

        // Usa el dueño comercial del cliente si no se indicó vendedor.
        if (!req.VendedorId.HasValue && cotizacion.Cliente?.VendedorId is int clienteId and > 0)
            vendedorId = clienteId;

        var sucursalId = ctx.ResolverSucursalAutorizada(cotizacion.SucursalId);
        var ahora = DateTime.UtcNow;
        var folioOrden = AsignarFolioOrdenVenta(sucursalId, ahora);

        var orden = new OrdenVenta
        {
            SucursalId = sucursalId,
            ClienteId = cotizacion.ClienteId,
            VendedorId = vendedorId,
            FechaEmision = ahora,
            CotizacionId = cotizacion.Id,
            Notas = req.Motivo?.Trim(),
            Estado = EstadoOrdenVenta.Borrador,
            UsuarioId = ctx.UsuarioId,
            FolioInterno = folioOrden,
            Detalles = ClonarDetalles(cotizacion.Detalles)
        };
        var totalesOrden = CalcularTotalesOrden(orden.Detalles);
        orden.Subtotal = totalesOrden.Subtotal;
        orden.Iva = totalesOrden.Iva;
        orden.Total = totalesOrden.Total;

        db.OrdenesVenta.Add(orden);
        cotizacion.Estado = EstadoCotizacion.Convertida;
        cotizacion.FechaVencimiento = null; // la cotización dejó de ser válida al convertirse
        await db.SaveChangesAsync(ct);

        return new CotizacionConvertirResult(orden.Id, orden.FolioInterno!, orden.Total, orden.Estado.ToString());
    }

    /// <summary>Anula una cotización (solo estado Borrador/Enviada/Cancelada).</summary>
    public async Task<OrdenResultado> CancelarCotizacionAsync(int id, string motivo, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La cotización no existe.");

        if (cotizacion.Estado is EstadoCotizacion.Expirada or EstadoCotizacion.Convertida)
            throw new InvalidOperationException("No se puede cancelar una cotización expirada o convertida.");

        cotizacion.Estado = EstadoCotizacion.Cancelada;
        cotizacion.Notas = cotizacion.Notas + (cotizacion.Notas is null or "" ? "" : Environment.NewLine) + $"Cancelada: {motivo}";
        cotizacion.FechaVencimiento = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new OrdenResultado(cotizacion.Id, cotizacion.FolioInterno ?? string.Empty, cotizacion.Estado.ToString(), "cotizacion");
    }

    /// <summary>Anula una orden de venta (solo estado Borrador/Confirmada/Suspendida).</summary>
    public async Task<OrdenResultado> CancelarOrdenVentaAsync(int id, string motivo, CancellationToken ct = default)
    {
        var orden = await db.OrdenesVenta.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La orden de venta no existe.");

        if (orden.Estado is EstadoOrdenVenta.Cancelada)
            throw new InvalidOperationException("La orden de venta ya fue cancelada.");

        orden.Estado = EstadoOrdenVenta.Cancelada;
        orden.Notas = orden.Notas + (orden.Notas is null or "" ? "" : Environment.NewLine) + $"Cancelada: {motivo}";

        await db.SaveChangesAsync(ct);

        return new OrdenResultado(orden.Id, orden.FolioInterno ?? string.Empty, orden.Estado.ToString(), "orden_venta");
    }
}

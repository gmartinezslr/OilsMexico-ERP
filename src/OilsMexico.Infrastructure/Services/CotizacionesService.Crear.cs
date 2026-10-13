using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CotizacionesService
{
    public async Task<CotizacionCrearResult> CrearCotizacionAsync(CotizacionCrearRequest req, CancellationToken ct = default)
    {
        var cliente = await db.Clientes.FindAsync([req.ClienteId], ct)
            ?? throw new InvalidOperationException("El cliente no existe.");
        if (!cliente.Activo) throw new InvalidOperationException("El cliente está inactivo.");

        int? vendedorId = req.VendedorId;
        if (vendedorId is not null)
        {
            var vendedor = await db.Usuarios.FindAsync([vendedorId.Value], ct)
                ?? throw new InvalidOperationException("El vendedor indicado no existe.");
            if (!vendedor.Activo) throw new InvalidOperationException("El vendedor indicado está inactivo.");
        }

        if (req.Detalles.Count == 0)
            throw new InvalidOperationException("La cotización debe tener al menos un renglón.");

        var detalles = new List<CotizacionDetalle>();
        foreach (var r in req.Detalles)
        {
            if (r.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
            if (r.PrecioUnitario < 0) throw new InvalidOperationException("El precio no puede ser negativo.");
            if (r.Descuento < 0) throw new InvalidOperationException("El descuento no puede ser negativo.");

            var producto = await db.Productos.FindAsync([r.ProductoId], ct)
                ?? throw new InvalidOperationException($"Producto {r.ProductoId} no existe.");
            if (!producto.Activo) throw new InvalidOperationException($"El producto {r.ProductoId} está inactivo.");

            var unidad = await ResolverUnidadAsync(r.ProductoId, r.UnidadMedidaId, ct);
            detalles.Add(new CotizacionDetalle
            {
                ProductoId = r.ProductoId,
                UnidadMedidaId = unidad.Id,
                UnidadNombre = unidad.UnidadNombre,
                Cantidad = r.Cantidad,
                PrecioUnitario = r.PrecioUnitario,
                Descuento = r.Descuento,
                Importe = CalcularImporte(r.Cantidad, r.PrecioUnitario, r.Descuento)
            });
        }

        var sucursalId = ctx.SucursalId;
        var ahora = DateTime.UtcNow;
        if (req.FechaVencimiento.HasValue && req.FechaVencimiento <= ahora)
            throw new InvalidOperationException("La fecha de vencimiento debe ser posterior a la de emisión.");

        var cotizacion = new Cotizacion
        {
            SucursalId = sucursalId,
            ClienteId = cliente.Id,
            VendedorId = vendedorId,
            FechaEmision = ahora,
            FechaVencimiento = req.FechaVencimiento,
            Notas = req.Notas?.Trim(),
            Estado = EstadoCotizacion.Borrador,
            UsuarioId = ctx.UsuarioId,
            Detalles = detalles,
            Subtotal = CalcularTotales(detalles).Subtotal,
            Iva = CalcularTotales(detalles).Iva,
            Total = CalcularTotales(detalles).Total,
            FolioInterno = AsignarFolioInterno(sucursalId, ahora)
        };

        db.Cotizaciones.Add(cotizacion);
        await db.SaveChangesAsync(ct);

        return new CotizacionCrearResult(cotizacion.Id, cotizacion.FolioInterno!);


    }
    public async Task<CotizacionResponseDto?> ActualizarCotizacionAsync(int id, CotizacionCrearRequest req, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones
            .Include(c => c.Cliente)
            .Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new InvalidOperationException("La cotizacion no existe.");

        if (cotizacion.Estado is EstadoCotizacion.Expirada or EstadoCotizacion.Convertida)
            throw new InvalidOperationException("No puedes modificar una cotizacion expirada o convertida.");

        if (cotizacion.Estado == EstadoCotizacion.Cancelada && req.Detalles.Count > 0)
            throw new InvalidOperationException("Una cotizacion cancelada no se puede reabrir con nuevos renglones.");

        var cliente = await db.Clientes.FindAsync([req.ClienteId], ct)
            ?? throw new InvalidOperationException("El cliente no existe.");
        if (!cliente.Activo) throw new InvalidOperationException("El cliente esta inactivo.");

        int? vendedorId = req.VendedorId;
        if (vendedorId is not null)
        {
            var vendedor = await db.Usuarios.FindAsync([vendedorId.Value], ct)
                ?? throw new InvalidOperationException("El vendedor indicado no existe.");
            if (!vendedor.Activo) throw new InvalidOperationException("El vendedor indicado esta inactivo.");
        }

        if (req.Detalles.Count == 0)
            throw new InvalidOperationException("La cotizacion debe tener al menos un renglon.");

        cotizacion.ClienteId = cliente.Id;
        cotizacion.VendedorId = vendedorId;
        cotizacion.FechaVencimiento = req.FechaVencimiento;
        cotizacion.Notas = req.Notas?.Trim();
        cotizacion.FolioInterno ??= AsignarFolioInterno(cotizacion.SucursalId, DateTime.UtcNow);

        foreach (var viejo in cotizacion.Detalles.ToList())
            db.CotizacionDetalles.Remove(viejo);
        cotizacion.Detalles.Clear();

        foreach (var r in req.Detalles)
        {
            if (r.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
            if (r.PrecioUnitario < 0) throw new InvalidOperationException("El precio no puede ser negativo.");
            if (r.Descuento < 0) throw new InvalidOperationException("El descuento no puede ser negativo.");

            var producto = await db.Productos.FindAsync([r.ProductoId], ct)
                ?? throw new InvalidOperationException("Producto " + r.ProductoId + " no existe.");
            if (!producto.Activo) throw new InvalidOperationException("El producto " + r.ProductoId + " esta inactivo.");

            var unidad = await ResolverUnidadAsync(r.ProductoId, r.UnidadMedidaId, ct);
            cotizacion.Detalles.Add(new CotizacionDetalle
            {
                ProductoId = r.ProductoId,
                UnidadMedidaId = unidad.Id,
                UnidadNombre = unidad.UnidadNombre,
                Cantidad = r.Cantidad,
                PrecioUnitario = r.PrecioUnitario,
                Descuento = r.Descuento,
                Importe = CalcularImporte(r.Cantidad, r.PrecioUnitario, r.Descuento)
            });
        }

        var totales = CalcularTotales(cotizacion.Detalles);
        cotizacion.Subtotal = totales.Subtotal;
        cotizacion.Iva = totales.Iva;
        cotizacion.Total = totales.Total;

        await db.SaveChangesAsync(ct);

        return await ObtenerCotizacionAsync(id, ct);
    }

}



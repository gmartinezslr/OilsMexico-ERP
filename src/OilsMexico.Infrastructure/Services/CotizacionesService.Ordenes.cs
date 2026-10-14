using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CotizacionesService
{
    // ---- catálogos para las pantallas de cotización / orden ----

    /// <summary>Productos activos con su unidad default y stock de la sucursal (reutiliza la query del POS).</summary>
    public async Task<List<ProductoDto>> ListarProductosAsync(int sucursalId, CancellationToken ct = default)
    {
        var q = from p in db.Productos
                where p.Activo
                select new
                {
                    p,
                    Unidad = db.UnidadesMedida
                        .Where(u => u.ProductoId == p.Id)
                        .OrderBy(u => u.UnidadNombre != "Litro").ThenBy(u => u.Id)
                        .FirstOrDefault(),
                    Stock = db.InventarioSucursal
                        .Where(i => i.SucursalId == sucursalId && i.ProductoId == p.Id)
                        .Sum(i => (decimal?)i.StockActual) ?? 0m
                };

        return await q.Select(x => new ProductoDto(
            x.p.Id, x.p.Sku, x.p.Nombre, x.p.Marca, x.p.Viscosidad, x.p.TipoBase,
            x.p.PrecioVenta, x.p.PrecioMayoreo, x.Stock,
            x.Unidad != null ? x.Unidad.UnidadNombre : "Litro",
            x.Unidad != null ? x.Unidad.FactorConversion : 1m,
            x.Unidad != null ? x.Unidad.Id : 0,
            x.Unidad != null ? x.Unidad.CodigoBarra : null))
            .Take(200).ToListAsync(ct);
    }

    /// <summary>Clientes activos (el catálogo de clientes no es por sucursal en el modelo actual).</summary>
    public Task<List<(int Id, string Nombre)>> ListarClientesAsync(int sucursalId, CancellationToken ct = default) =>
        db.Clientes.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new ValueTuple<int, string>(c.Id, c.Nombre))
            .ToListAsync(ct);

    /// <summary>Vendedores (usuarios con rol Vendedor, activos) de la sucursal.</summary>
    public Task<List<(int Id, string Nombre)>> ListarVendedoresAsync(int sucursalId, CancellationToken ct = default) =>
        db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && u.SucursalId == sucursalId && u.Rol == Roles.Vendedor)
            .OrderBy(u => u.Nombre)
            .Select(u => new ValueTuple<int, string>(u.Id, u.Nombre))
            .ToListAsync(ct);

    /// <summary>Resuelve la unidad de venta de un renglón: la indicada, o la default (Litro/primera) si no se especifica.</summary>
    private async Task<UnidadMedida> ResolverUnidadAsync(int productoId, int? unidadMedidaId, CancellationToken ct)
    {
        if (unidadMedidaId is > 0)
        {
            var exacta = await db.UnidadesMedida.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == unidadMedidaId.Value && u.ProductoId == productoId, ct)
                ?? throw new InvalidOperationException($"La unidad {unidadMedidaId} no pertenece al producto {productoId}.");
            return exacta;
        }

        var porDefecto = await db.UnidadesMedida.AsNoTracking()
            .Where(u => u.ProductoId == productoId)
            .OrderBy(u => u.UnidadNombre != "Litro").ThenBy(u => u.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"El producto {productoId} no tiene unidades de venta configuradas.");
        return porDefecto;
    }

    // ---- órdenes de venta ----

    /// <summary>Alta de orden de venta manual (no derivada de cotización).</summary>
    public async Task<OrdenVentaCrearResult> CrearOrdenVentaAsync(OrdenVentaCrearRequest req, CancellationToken ct = default)
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

        // Dueño comercial del cliente si no se indicó vendedor.
        if (!vendedorId.HasValue && cliente.VendedorId is int duenoId and > 0)
            vendedorId = duenoId;

        if (req.Detalles.Count == 0)
            throw new InvalidOperationException("La orden de venta debe tener al menos un renglón.");

        var detalles = new List<OrdenVentaDetalle>();
        foreach (var r in req.Detalles)
        {
            if (r.Cantidad <= 0) throw new InvalidOperationException("La cantidad debe ser mayor a cero.");
            if (r.PrecioUnitario < 0) throw new InvalidOperationException("El precio no puede ser negativo.");
            if (r.Descuento < 0) throw new InvalidOperationException("El descuento no puede ser negativo.");

            var producto = await db.Productos.FindAsync([r.ProductoId], ct)
                ?? throw new InvalidOperationException($"Producto {r.ProductoId} no existe.");
            if (!producto.Activo) throw new InvalidOperationException($"El producto {r.ProductoId} está inactivo.");

            var unidad = await ResolverUnidadAsync(r.ProductoId, r.UnidadMedidaId, ct);
            detalles.Add(new OrdenVentaDetalle
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
        var orden = new OrdenVenta
        {
            SucursalId = sucursalId,
            ClienteId = cliente.Id,
            VendedorId = vendedorId,
            FechaEmision = ahora,
            CotizacionId = req.CotizacionId,
            Notas = req.Notas?.Trim(),
            Estado = EstadoOrdenVenta.Borrador,
            UsuarioId = ctx.UsuarioId,
            FolioInterno = string.IsNullOrWhiteSpace(req.FolioInterno) ? AsignarFolioOrdenVenta(sucursalId, ahora) : req.FolioInterno,
            Detalles = detalles
        };
        var totales = CalcularTotalesOrden(detalles);
        orden.Subtotal = totales.Subtotal;
        orden.Iva = totales.Iva;
        orden.Total = totales.Total;

        db.OrdenesVenta.Add(orden);
        await db.SaveChangesAsync(ct);

        return new OrdenVentaCrearResult(orden.Id, orden.FolioInterno!);
    }

    /// <summary>Confirma una orden de venta: fija el vendedor y pasa a Confirmada.</summary>
    public async Task<OrdenVentaConfirmarResult> ConfirmarOrdenVentaAsync(int id, OrdenVentaConfirmarRequest req, CancellationToken ct = default)
    {
        var orden = await db.OrdenesVenta.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La orden de venta no existe.");

        if (orden.SucursalId != ctx.SucursalId)
            throw new InvalidOperationException("La orden pertenece a otra sucursal; cambia a esa sucursal para continuar.");
        if (orden.Estado is EstadoOrdenVenta.Cancelada or EstadoOrdenVenta.Confirmada or EstadoOrdenVenta.Facturada)
            throw new InvalidOperationException("La orden de venta no se puede confirmar en su estado actual.");

        if (req.VendedorId is int vendId)
        {
            var vendedor = await db.Usuarios.FindAsync([vendId], ct)
                ?? throw new InvalidOperationException("El vendedor indicado no existe.");
            if (!vendedor.Activo) throw new InvalidOperationException("El vendedor indicado está inactivo.");
            orden.VendedorId = vendId;
        }

        var estadoAnterior = orden.Estado;
        orden.Estado = EstadoOrdenVenta.Confirmada;
        if (!string.IsNullOrWhiteSpace(req.Motivo))
            orden.Notas = orden.Notas + (orden.Notas is null or "" ? "" : Environment.NewLine) + $"Confirmada: {req.Motivo}";

        RegistrarAuditoriaOrdenVenta(orden, estadoAnterior, "OVConfirmada", req.Motivo);
        await db.SaveChangesAsync(ct);

        return new OrdenVentaConfirmarResult(orden.Id, orden.FolioInterno!, orden.Estado);
    }

    /// <summary>
    /// Suspende una orden Confirmada (bloqueo temporal en revisión, Capa C).
    /// Mientras esté Suspendida no se puede facturar; se reanuda para volver a Confirmada.
    /// </summary>
    public async Task<OrdenResultado> SuspenderOrdenVentaAsync(int id, string? motivo, CancellationToken ct = default)
    {
        var orden = await db.OrdenesVenta.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La orden de venta no existe.");

        if (orden.SucursalId != ctx.SucursalId)
            throw new InvalidOperationException("La orden pertenece a otra sucursal; cambia a esa sucursal para continuar.");
        if (orden.Estado != EstadoOrdenVenta.Confirmada)
            throw new InvalidOperationException("Solo se pueden suspender órdenes en estado Confirmada.");

        var estadoAnterior = orden.Estado;
        orden.Estado = EstadoOrdenVenta.Suspendida;
        if (!string.IsNullOrWhiteSpace(motivo))
            orden.Notas = orden.Notas + (orden.Notas is null or "" ? "" : Environment.NewLine) + $"Suspendida: {motivo}";

        RegistrarAuditoriaOrdenVenta(orden, estadoAnterior, "OVSuspendida", motivo);
        await db.SaveChangesAsync(ct);

        return new OrdenResultado(orden.Id, orden.FolioInterno ?? string.Empty, orden.Estado.ToString(), "orden_venta");
    }

    /// <summary>
    /// Reanuda una orden Suspendida (Capa C): regresa a Confirmada con su precio de compromiso intacto,
    /// por lo que vuelve a ser facturable.
    /// </summary>
    public async Task<OrdenResultado> ReanudarOrdenVentaAsync(int id, string? motivo, CancellationToken ct = default)
    {
        var orden = await db.OrdenesVenta.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La orden de venta no existe.");

        if (orden.SucursalId != ctx.SucursalId)
            throw new InvalidOperationException("La orden pertenece a otra sucursal; cambia a esa sucursal para continuar.");
        if (orden.Estado != EstadoOrdenVenta.Suspendida)
            throw new InvalidOperationException("Solo se pueden reanudar órdenes en estado Suspendida.");

        var estadoAnterior = orden.Estado;
        orden.Estado = EstadoOrdenVenta.Confirmada;
        if (!string.IsNullOrWhiteSpace(motivo))
            orden.Notas = orden.Notas + (orden.Notas is null or "" ? "" : Environment.NewLine) + $"Reanudada: {motivo}";

        RegistrarAuditoriaOrdenVenta(orden, estadoAnterior, "OVReanudada", motivo);
        await db.SaveChangesAsync(ct);

        return new OrdenResultado(orden.Id, orden.FolioInterno ?? string.Empty, orden.Estado.ToString(), "orden_venta");
    }

    private void RegistrarAuditoriaOrdenVenta(
        OrdenVenta orden, EstadoOrdenVenta estadoAnterior, string tipo, string? motivo)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UsuarioId = ctx.UsuarioId,
            Tipo = tipo,
            Detalle = $"Orden {orden.FolioInterno ?? orden.Id.ToString()}" +
                (string.IsNullOrWhiteSpace(motivo) ? "" : $". Motivo: {motivo.Trim()}"),
            Anterior = estadoAnterior.ToString(),
            Nuevo = orden.Estado.ToString(),
            FechaUtc = DateTime.UtcNow
        });
    }
}

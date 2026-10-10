using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

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
        var vendedorId = await ResolverVendedorAsync(req.VendedorId, cliente.VendedorId, ct);
        var factura = new Factura
        {
            SucursalId = sucursalId,
            FolioInterno = $"V-{sucursalId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ClienteId = cliente.Id, FormaPagoSat = req.FormaPagoSat,
            MetodoPagoSat = req.MetodoPagoSat, UsoCfdi = req.UsoCfdi,
            VendedorId = vendedorId,
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
        factura.Subtotal = Impuestos.BaseDeTotal(subtotal);
        factura.Iva = Impuestos.IvaDeTotal(subtotal);
        factura.Total = Math.Round(subtotal, 2);
        db.Facturas.Add(factura);
        await db.SaveChangesAsync(ct);

        // CASH BASIS: la venta PUE (pago en una sola exhibición / contado) ya está cobrada al
        // emitirse, así que se estampa su cobro automático. Deja PUE y PPD con UNA sola fuente
        // de verdad (venta_cobros): comisiones, estado de cuenta y cortes leen lo mismo.
        // Las PPD siguen cobrándose por REP en ComplementoPagoService (que rechaza facturas PUE,
        // por lo que aquí no puede haber doble cobro).
        if (factura.MetodoPagoSat == "PUE")
        {
            db.VentaCobros.Add(new VentaCobro
            {
                SucursalId = factura.SucursalId,
                FacturaId = factura.Id,
                ClienteId = factura.ClienteId,
                Monto = factura.Total,
                FormaPagoSat = factura.FormaPagoSat,
                FechaPagoUtc = factura.FechaEmision,
                Referencia = "PUE-CONTADO",
                UsuarioId = ctx.UsuarioId
            });
            await db.SaveChangesAsync(ct);
        }

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

    /// <summary>
    /// Resuelve quién se lleva el crédito de la venta. Cadena de precedencia:
    /// 1) el vendedor elegido explícitamente en la UI (si es inválido se rechaza la venta, no se
    ///    silencia: así no se redirigen comisiones por error o por manipulación del request),
    /// 2) el dueño comercial ACTUAL del cliente (si sigue activo),
    /// 3) el usuario de sesión (mostrador / cajero, o fallback si el dueño del cliente ya no está activo).
    /// El resultado se congela en Factura.VendedorId: reasignar el cliente después NO lo cambia.
    /// </summary>
    private async Task<int> ResolverVendedorAsync(int? vendedorSolicitado, int? vendedorCliente, CancellationToken ct)
    {
        if (vendedorSolicitado.HasValue)
        {
            if (!await EsVendedorValidoAsync(vendedorSolicitado.Value, ct))
                throw new InvalidOperationException(
                    "El vendedor seleccionado no existe, no está activo o no tiene rol Vendedor/Admin.");
            return vendedorSolicitado.Value;
        }

        if (vendedorCliente.HasValue && await EsVendedorValidoAsync(vendedorCliente.Value, ct))
            return vendedorCliente.Value;

        return ctx.UsuarioId;
    }

    /// <summary>Un vendedor sólo puede recibir crédito si el usuario existe, está activo y su rol lo permite.</summary>
    private async Task<bool> EsVendedorValidoAsync(int usuarioId, CancellationToken ct) =>
        await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId && u.Activo
            && (u.Rol == "Vendedor" || u.Rol == "Admin"), ct);
}

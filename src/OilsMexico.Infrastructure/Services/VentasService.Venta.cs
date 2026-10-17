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
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var factura = await CrearVentaEnTransaccionAsync(req, ct);
        await tx.CommitAsync(ct);
        var uuid = req.RequiereFactura ? await TimbrarFacturaAsync(factura, ct) : null;
        return ResultadoVenta(factura, uuid);
    }

    public async Task<OrdenVentaFacturarResult> FacturarOrdenVentaAsync(
        int ordenId, OrdenVentaFacturarRequest req, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var orden = await db.OrdenesVenta
            .Include(o => o.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(o => o.Id == ordenId, ct)
            ?? throw new InvalidOperationException("La orden de venta no existe.");

        if (orden.Estado != EstadoOrdenVenta.Confirmada)
            throw new InvalidOperationException("Solo las órdenes en estado Confirmada se pueden facturar.");
        if (orden.FacturaId.HasValue)
            throw new InvalidOperationException("La orden ya fue facturada.");
        if (!ctx.EsAdmin && orden.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes facturar órdenes de otra sucursal.");
        if (orden.Detalles.Count == 0)
            throw new InvalidOperationException("La orden no tiene renglones para facturar.");

        // Prioridad 1.1: la facturación genera venta + póliza + kardex, así que respeta el
        // periodo cerrado (igual que el POS). Cotizaciones/Órdenes previas no se bloquean.
        var hoyLocal = ReglaPeriodoCerrado.ALocal(DateTime.UtcNow);
        if (await _cierreContable.EstaCerradoAsync(orden.SucursalId, hoyLocal, ct))
            throw new InvalidOperationException(ReglaPeriodoCerrado.Mensaje(hoyLocal));

        var formaPago = string.IsNullOrWhiteSpace(req.FormaPagoSat)
            ? CfdiDefaults.FormaPagoVenta : req.FormaPagoSat.Trim();
        var metodoPago = string.IsNullOrWhiteSpace(req.MetodoPagoSat)
            ? CfdiDefaults.MetodoPago : req.MetodoPagoSat.Trim();
        var usoCfdi = string.IsNullOrWhiteSpace(req.UsoCfdi)
            ? CfdiDefaults.UsoCfdiVenta : req.UsoCfdi.Trim();

        var unidadIds = orden.Detalles.Select(d => d.UnidadMedidaId).Distinct().ToArray();
        var factores = await db.UnidadesMedida.AsNoTracking()
            .Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FactorConversion, ct);
        var items = new List<CarritoItemDto>(orden.Detalles.Count);
        foreach (var detalle in orden.Detalles.OrderBy(d => d.Id))
        {
            if (detalle.Cantidad <= 0)
                throw new InvalidOperationException("La orden tiene renglones con cantidad inválida.");
            if (detalle.Importe <= 0)
                throw new InvalidOperationException($"El renglón del producto {detalle.ProductoId} no tiene importe válido.");

            var precioConIva = Math.Round(detalle.Importe * (1m + Impuestos.TasaIva) / detalle.Cantidad, 4);
            items.Add(new CarritoItemDto(
                detalle.ProductoId, detalle.Producto?.Sku ?? string.Empty, detalle.Producto?.Nombre ?? string.Empty,
                detalle.UnidadMedidaId, detalle.UnidadNombre, factores.GetValueOrDefault(detalle.UnidadMedidaId, 1m),
                detalle.Cantidad, precioConIva));
        }

        var factura = await CrearVentaEnTransaccionAsync(new VentaPosRequest(
            orden.SucursalId, orden.ClienteId, ctx.UsuarioId, orden.VendedorId,
            formaPago, metodoPago, usoCfdi, req.RequiereFactura, items, CongelarPrecios: true), ct);

        var estadoAnterior = orden.Estado;
        orden.FacturaId = factura.Id;
        orden.Estado = EstadoOrdenVenta.Facturada;
        orden.Notas = orden.Notas + (string.IsNullOrEmpty(orden.Notas) ? "" : Environment.NewLine)
            + $"Facturada: {factura.FolioInterno}";
        db.AuditLogs.Add(new AuditLog
        {
            UsuarioId = ctx.UsuarioId,
            Tipo = "OVFacturada",
            Detalle = $"Orden {orden.FolioInterno ?? orden.Id.ToString()}. Facturada: {factura.FolioInterno}",
            Anterior = estadoAnterior.ToString(),
            Nuevo = orden.Estado.ToString(),
            FechaUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var uuid = req.RequiereFactura ? await TimbrarFacturaAsync(factura, ct) : null;
        var venta = ResultadoVenta(factura, uuid);
        return new OrdenVentaFacturarResult(
            orden.Id, orden.FolioInterno ?? string.Empty, orden.Estado,
            venta.FacturaId, venta.FolioInterno, venta.Estado,
            venta.Subtotal, venta.Iva, venta.Total, venta.UuidSat);
    }

    private async Task<Factura> CrearVentaEnTransaccionAsync(VentaPosRequest req, CancellationToken ct)
    {
        var sucursalId = ctx.SucursalId;
        if (req.Items.Count == 0) throw new InvalidOperationException("Carrito vacío.");
        var cliente = await db.Clientes.FindAsync([req.ClienteId], ct)
            ?? throw new InvalidOperationException("Cliente no existe.");
        var fechaEmision = DateTime.UtcNow;
        // Prioridad 1.1 (ReglaPeriodoCerrado): ninguna venta en mes cerrado; la vía es reabrir (solo Admin).
        var fechaLocal = ReglaPeriodoCerrado.ALocal(fechaEmision);
        if (await _cierreContable.EstaCerradoAsync(ctx.SucursalId, fechaLocal, ct))
            throw new InvalidOperationException(ReglaPeriodoCerrado.Mensaje(fechaLocal));
        var vendedorId = await ResolverVendedorAsync(req.VendedorId, cliente.VendedorId, ct);
        if (cliente.LimiteCredito < 0m)
            throw new InvalidOperationException("El límite de crédito del cliente no puede ser negativo.");
        if (string.Equals(req.MetodoPagoSat, "PPD", StringComparison.OrdinalIgnoreCase)
            && cliente.LimiteCredito == 0m)
            throw new InvalidOperationException(
                $"El cliente \"{cliente.Nombre}\" no tiene crédito habilitado; registra la venta de contado (PUE).");
        var saldoVencido = await _cobranza.SaldoVencidoPorClienteAsync(cliente.Id, ct);
        if (saldoVencido > cliente.LimiteCredito)
            throw new InvalidOperationException(
                $"Cliente \"{cliente.Nombre}\" tiene saldo vencido de ${saldoVencido:0.00} que supera su límite de crédito de ${cliente.LimiteCredito:0.00}. Acuda a la sección de cobranza para regularizar.");
        var factura = new Factura
        {
            SucursalId = sucursalId,
            FolioInterno = $"V-{sucursalId}-{fechaEmision:yyyyMMddHHmmss}",
            ClienteId = cliente.Id, FormaPagoSat = req.FormaPagoSat,
            MetodoPagoSat = req.MetodoPagoSat, UsoCfdi = req.UsoCfdi,
            VendedorId = vendedorId, FechaEmision = fechaEmision,
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
            // CongelarPrecios (orden de venta facturada): el precio del renglón es de compromiso
            // y NO se sustituye por el precio mayoreo de catálogo del cliente.
            var precioEscalon = !req.CongelarPrecios
                ? await (from lista in db.ListasPrecio
                         join regla in db.PreciosVolumen on lista.Id equals regla.ListaPrecioId
                         where lista.Activa && lista.TipoCliente == cliente.TipoPrecio
                             && regla.ProductoId == item.ProductoId && regla.CantidadMinima <= item.Cantidad * factor
                         orderby regla.CantidadMinima descending
                         select (decimal?)regla.PrecioUnitario).FirstOrDefaultAsync(ct)
                : null;
            var precio = req.CongelarPrecios
                ? item.PrecioUnitario
                : precioEscalon ?? (cliente.TipoPrecio == "mayoreo"
                    ? (prod?.PrecioMayoreo ?? item.PrecioUnitario) : item.PrecioUnitario);
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
        await RegistrarPolizaVentaAsync(factura, ct);
        await db.SaveChangesAsync(ct);
        return factura;
    }

    private async Task RegistrarPolizaVentaAsync(Factura factura, CancellationToken ct)
    {
        var reglasConfiguradas = await db.ReglasAsiento.AsNoTracking()
            .Where(r => r.Evento == "Venta" && r.Activo)
            .OrderBy(r => r.Orden)
            .Select(r => new { r.CuentaId, r.Debe, r.MontoOrigen })
            .ToListAsync(ct);
        var cuentas = await db.CuentasContables
            .Where(c => c.SucursalId == factura.SucursalId)
            .ToListAsync(ct);
        var porId = cuentas.ToDictionary(c => c.Id);
        var reglas = reglasConfiguradas.Where(r => porId.ContainsKey(r.CuentaId)).ToList();
        if (reglas.Count == 0)
        {
            await CuentasContablesService.AsegurarCatalogoAsync(db, factura.SucursalId, ct);
            cuentas = await db.CuentasContables
                .Where(c => c.SucursalId == factura.SucursalId)
                .ToListAsync(ct);
            porId = cuentas.ToDictionary(c => c.Id);
            reglas = reglasConfiguradas.Where(r => porId.ContainsKey(r.CuentaId)).ToList();
        }

        var detalles = reglas.Count > 0
            ? reglas.Select(r => (
                Cuenta: porId.GetValueOrDefault(r.CuentaId),
                Debe: r.Debe,
                Importe: r.MontoOrigen switch
                {
                    "total" => factura.Total,
                    "subtotal" or "neto" => factura.Subtotal,
                    "iva" => factura.Iva,
                    _ => 0m
                }))
                .Where(x => x.Importe > 0m)
                .ToList()
            : CuentasPredeterminadasVenta(cuentas, factura);

        if (detalles.Any(d => d.Cuenta is null))
            throw new InvalidOperationException("Una regla contable de Venta apunta a una cuenta inexistente o de otra sucursal.");
        var totalDebe = detalles.Where(d => d.Debe).Sum(d => d.Importe);
        var totalHaber = detalles.Where(d => !d.Debe).Sum(d => d.Importe);
        if (detalles.Count == 0 || Math.Abs(totalDebe - totalHaber) > 0.01m)
            throw new InvalidOperationException(
                $"Las reglas contables de Venta no están equilibradas: débitos {totalDebe:0.00} vs créditos {totalHaber:0.00}.");

        var asiento = new AsientoContable
        {
            SucursalId = factura.SucursalId,
            UsuarioId = ctx.UsuarioId,
            FechaUtc = factura.FechaEmision,
            Tipo = "Operativo",
            Concepto = $"Venta {factura.FolioInterno}",
            Detalles = detalles.Select(d => new DetalleAsiento
            {
                CuentaId = d.Cuenta!.Id,
                TipoMovimiento = d.Debe ? MovimientoTipo.Debito : MovimientoTipo.Credito,
                Importe = Math.Round(d.Importe, 2),
                Descripcion = $"Venta {factura.FolioInterno}"
            }).ToList()
        };
        db.AsientosContables.Add(asiento);
        foreach (var detalle in detalles)
        {
            detalle.Cuenta!.SaldoActual += detalle.Debe ? detalle.Importe : -detalle.Importe;
        }
    }

    private static List<(CuentaContable? Cuenta, bool Debe, decimal Importe)> CuentasPredeterminadasVenta(
        IReadOnlyCollection<CuentaContable> cuentas, Factura factura)
    {
        CuentaContable Obtener(string codigo) =>
            cuentas.FirstOrDefault(c => c.Codigo == codigo)
            ?? throw new InvalidOperationException($"No existe la cuenta contable predeterminada {codigo}.");

        var detalles = new List<(CuentaContable? Cuenta, bool Debe, decimal Importe)>
        {
            (Obtener(factura.MetodoPagoSat == "PUE" ? "1000" : "1100"), true, factura.Total),
            (Obtener("3000"), false, factura.Subtotal)
        };
        if (factura.Iva > 0m)
            detalles.Add((Obtener("4000"), false, factura.Iva));
        return detalles;
    }

    private async Task<Guid?> TimbrarFacturaAsync(Factura factura, CancellationToken ct)
    {
        var (cadenaOriginal, sello, xmlSellado) = await sellado.SellarAsync(factura.Id, ct);
        var (uuidTimbrado, xmlTimbrado) = await pac.TimbrarAsync(xmlSellado, ct);
        factura.CadenaOriginal = cadenaOriginal;
        factura.SelloDigital = sello;
        factura.UuidSat = uuidTimbrado;
        factura.XmlSellado = xmlTimbrado;
        factura.Estado = EstadoFactura.Timbrada;
        await db.SaveChangesAsync(ct);
        return uuidTimbrado;
    }

    private static VentaPosResult ResultadoVenta(Factura factura, Guid? uuid) =>
        new(factura.Id, factura.FolioInterno, uuid,
            factura.Subtotal, factura.Iva, factura.Total,
            factura.Estado);

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
            && (u.Rol == Roles.Vendedor || u.Rol == Roles.Admin), ct);

    /// <summary>Consulta el estado de crédito de un cliente para el bloqueo por morosidad (§4.1).</summary>
    public async Task<CreditoClienteDto?> ConsultarCreditoClienteAsync(int clienteId, CancellationToken ct = default)
    {
        var cliente = await db.Clientes.FindAsync([clienteId], ct);
        if (cliente is null) return null;

        var saldoVencido = await _cobranza.SaldoVencidoPorClienteAsync(clienteId, ct);
        var bloqueado = cliente.LimiteCredito > 0m && saldoVencido > cliente.LimiteCredito;

        return new CreditoClienteDto(
            cliente.Id, cliente.Nombre, cliente.LimiteCredito, cliente.DiasCredito,
            saldoVencido, bloqueado);
    }
}

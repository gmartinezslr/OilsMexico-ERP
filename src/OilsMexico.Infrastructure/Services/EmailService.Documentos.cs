using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Documentos del ERP enviados por correo: cotización, CFDI timbrado (XML adjunto),
/// estado de cuenta de cliente (CxC) y estado de cuenta de proveedor (CxP).
/// </summary>
public sealed partial class EmailService
{
    public async Task<EnvioCorreoResult> EnviarCotizacionAsync(int cotizacionId, string? correoDestino = null, CancellationToken ct = default)
    {
        // REGLA #1: una cotización de otra sucursal no es consultable/enviable.
        var c = await db.Cotizaciones.AsNoTracking()
            .Include(x => x.Sucursal).Include(x => x.Cliente).Include(x => x.Vendedor)
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(x => x.Id == cotizacionId, ct)
            ?? throw new InvalidOperationException("La cotización no existe.");
        if (!ctx.EsAdmin && c.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes enviar cotizaciones de otra sucursal.");

        var destino = ResolverDestino(correoDestino, c.Cliente?.Email, $"El cliente '{c.Cliente?.Nombre}'");
        var empresa = await EmpresaAsync(ct);

        var filas = c.Detalles.Select(d => new[]
        {
            WebUtility.HtmlEncode(d.Producto?.Sku ?? ""),
            WebUtility.HtmlEncode(d.Producto?.Nombre ?? ""),
            d.Cantidad.ToString("N2"),
            d.PrecioUnitario.ToString("C2"),
            d.Descuento.ToString("C2"),
            d.Importe.ToString("C2")
        });
        var renglones = TablaHtml(["SKU", "Producto", "Cant.", "P. unitario", "Desc.", "Importe"], filas);

        var cuerpo = new StringBuilder()
            .Append("<p>Estimado(a) <strong>").Append(WebUtility.HtmlEncode(c.Cliente?.Nombre ?? "cliente")).Append("</strong>:</p>")
            .Append("<p>Le compartimos la cotización <strong>").Append(WebUtility.HtmlEncode(c.FolioInterno ?? ""))
            .Append("</strong> del ").Append(c.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy")).Append(".<br/>")
            .Append("Vigencia: ").Append(c.FechaVencimiento?.ToLocalTime().ToString("dd/MM/yyyy") ?? "sin límite").Append(".</p>")
            .Append(renglones)
            .Append("<p style=\"text-align:right;margin-top:12px;\">Subtotal: <strong>").Append(c.Subtotal.ToString("C2")).Append("</strong><br/>")
            .Append("IVA: <strong>").Append(c.Iva.ToString("C2")).Append("</strong><br/>")
            .Append("Total: <strong style=\"font-size:16px;\">").Append(c.Total.ToString("C2")).Append("</strong></p>");
        if (!string.IsNullOrWhiteSpace(c.Notas))
            cuerpo.Append("<p><em>").Append(WebUtility.HtmlEncode(c.Notas)).Append("</em></p>");
        if (c.Vendedor is not null)
            cuerpo.Append("<p>Atendido por: ").Append(WebUtility.HtmlEncode(c.Vendedor.Nombre)).Append("</p>");

        var mensaje = new EmailMensajeDto(destino,
            $"Cotización {c.FolioInterno} — {empresa}",
            PlantillaHtml($"Cotización {c.FolioInterno}", cuerpo.ToString()),
            c.Cliente?.Nombre);
        await EnviarAsync(mensaje, ct);
        return new EnvioCorreoResult(true, $"Cotización {c.FolioInterno} enviada a {destino}.");
    }

    public async Task<EnvioCorreoResult> EnviarCfdiAsync(int facturaId, string? correoDestino = null, CancellationToken ct = default)
    {
        var f = await db.Facturas.AsNoTracking()
            .Include(x => x.Cliente).Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("La factura no existe.");
        // Mismo aislamiento que FacturacionService (REGLA #1).
        if (!ctx.EsAdmin && f.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes enviar facturas de otra sucursal.");
        if (string.IsNullOrEmpty(f.XmlSellado))
            throw new InvalidOperationException("La factura no tiene XML timbrado; timbra primero el CFDI para poder enviarlo.");
        if (f.Estado is EstadoFactura.Cancelada)
            throw new InvalidOperationException("La factura está cancelada; no se envía un CFDI cancelado al cliente.");

        var destino = ResolverDestino(correoDestino, f.Cliente?.Email, $"El cliente '{f.Cliente?.Nombre}'");
        var empresa = await EmpresaAsync(ct);
        var cuerpo = new StringBuilder()
            .Append("<p>Estimado(a) <strong>").Append(WebUtility.HtmlEncode(f.Cliente?.Nombre ?? "cliente")).Append("</strong>:</p>")
            .Append("<p>Adjuntamos el CFDI 4.0 de la factura <strong>").Append(WebUtility.HtmlEncode(f.FolioInterno))
            .Append("</strong> por <strong>").Append(f.Total.ToString("C2")).Append("</strong>.</p>")
            .Append(TablaHtml(["UUID SAT", "Fecha", "Total"], [new[]
            {
                WebUtility.HtmlEncode(f.UuidSat?.ToString().ToUpperInvariant() ?? "—"),
                f.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                f.Total.ToString("C2")
            }]))
            .Append("<p style=\"font-size:12px;color:#6c757d;\">El archivo .xml adjunto es su comprobante fiscal electrónico.</p>");

        var mensaje = new EmailMensajeDto(destino,
            $"CFDI {f.FolioInterno} — {empresa}",
            PlantillaHtml($"CFDI {f.FolioInterno}", cuerpo.ToString()),
            f.Cliente?.Nombre,
            [new EmailAdjuntoDto($"{f.FolioInterno}.xml", Encoding.UTF8.GetBytes(f.XmlSellado), "application/xml")]);
        await EnviarAsync(mensaje, ct);
        return new EnvioCorreoResult(true, $"CFDI {f.FolioInterno} enviado a {destino} (XML adjunto).");
    }

    public async Task<EnvioCorreoResult?> EnviarCfdiAutomaticoAsync(int facturaId, CancellationToken ct = default)
    {
        // Hook de "Envío automático de CFDI por correo": solo actúa con la clave activa y
        // correo del cliente; cualquier fallo se traga para no romper el timbrado.
        try
        {
            var config = await ObtenerConfigAsync(ct);
            if (!config.Habilitado || !config.EnvioAutomaticoCfdi) return null;
            var clienteEmail = await db.Facturas.AsNoTracking()
                .Where(f => f.Id == facturaId)
                .Select(f => f.Cliente != null ? f.Cliente.Email : null)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(clienteEmail)) return null;
            return await EnviarCfdiAsync(facturaId, null, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Correo] Auto-envio de CFDI fallido (factura {facturaId}): {ex.Message}");
            return null;
        }
    }

    public async Task<EnvioCorreoResult> EnviarEstadoCuentaClienteAsync(int clienteId, string? correoDestino = null, CancellationToken ct = default)
    {
        // Reutiliza EstadoCuentasService (mismo criterio cash-basis de cobranza, REGLA #1 incluida).
        var ec = await estadoCuentas.ObtenerEstadoCuentaAsync(clienteId, ct)
            ?? throw new InvalidOperationException("El cliente no existe o no tiene saldo pendiente.");
        var destino = ResolverDestino(correoDestino, ec.Email, $"El cliente '{ec.Nombre}'");
        var empresa = await EmpresaAsync(ct);

        var filasFac = ec.FacturasAbiertas.Select(f => new[]
        {
            WebUtility.HtmlEncode(f.FolioInterno),
            f.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy"),
            f.Estado.ToString(),
            f.Total.ToString("C2"),
            WebUtility.HtmlEncode(f.UuidSat?.ToString().ToUpperInvariant() ?? "—")
        });
        var filasPagos = ec.Pagos.Select(p => new[]
        {
            p.Fecha.ToLocalTime().ToString("dd/MM/yyyy"),
            WebUtility.HtmlEncode(p.Tipo),
            WebUtility.HtmlEncode(p.Referencia),
            p.Monto.ToString("C2")
        });

        var cuerpo = new StringBuilder()
            .Append("<p>Estimado(a) <strong>").Append(WebUtility.HtmlEncode(ec.Nombre)).Append("</strong> (RFC ")
            .Append(WebUtility.HtmlEncode(ec.RFC)).Append("):</p>")
            .Append("<p>Le compartimos su estado de cuenta al ").Append(DateTime.Now.ToString("dd/MM/yyyy")).Append(".</p>")
            .Append("<h4 style=\"margin:12px 0 4px;\">Facturas abiertas</h4>")
            .Append(TablaHtml(["Folio", "Fecha", "Estado", "Total", "UUID SAT"], filasFac))
            .Append("<h4 style=\"margin:12px 0 4px;\">Pagos aplicados</h4>")
            .Append(ec.Pagos.Count > 0 ? TablaHtml(["Fecha", "Tipo", "Referencia", "Monto"], filasPagos)
                : "<p><em>Sin pagos registrados.</em></p>")
            .Append("<p style=\"text-align:right;margin-top:12px;\">Saldo pendiente: <strong style=\"font-size:16px;color:#dc3545;\">")
            .Append(ec.SaldoPendiente.ToString("C2")).Append("</strong></p>");

        var mensaje = new EmailMensajeDto(destino,
            $"Estado de cuenta — {ec.Nombre} — {empresa}",
            PlantillaHtml("Estado de cuenta", cuerpo.ToString()), ec.Nombre);
        await EnviarAsync(mensaje, ct);
        return new EnvioCorreoResult(true, $"Estado de cuenta enviado a {destino} (saldo {ec.SaldoPendiente:C2}).");
    }

    public async Task<EnvioCorreoResult> EnviarEstadoCuentaProveedorAsync(int proveedorId, string? correoDestino = null, CancellationToken ct = default)
    {
        var prov = await db.Proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proveedorId, ct)
            ?? throw new InvalidOperationException("El proveedor no existe.");
        var destino = ResolverDestino(correoDestino, prov.Email, $"El proveedor '{prov.Nombre}'");

        // CxP: compras no canceladas con saldo (mismo criterio que ComprasService.CuentasPorPagarAsync),
        // pero global al proveedor (una factura del proveedor puede abarcar varias sucursales).
        var compras = await db.Compras.AsNoTracking()
            .Where(c => c.ProveedorId == proveedorId && c.Estado != EstadoCompra.Cancelada
                && c.Total - c.MontoPagado > 0.01m)
            .OrderBy(c => c.FechaEmision)
            .ToListAsync(ct);
        if (compras.Count == 0)
            throw new InvalidOperationException("El proveedor no tiene cuentas por pagar abiertas; no hay estado de cuenta que enviar.");

        var pagos = await db.CompraPagos.AsNoTracking()
            .Where(p => compras.Select(c => c.Id).Contains(p.CompraId))
            .OrderBy(p => p.FechaUtc)
            .ToListAsync(ct);
        var saldo = compras.Sum(c => c.Total - c.MontoPagado);
        var empresa = await EmpresaAsync(ct);

        var filas = compras.Select(c => new[]
        {
            WebUtility.HtmlEncode(c.FolioInterno),
            WebUtility.HtmlEncode(c.FolioProveedor ?? "—"),
            c.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy"),
            c.Total.ToString("C2"),
            c.MontoPagado.ToString("C2"),
            (c.Total - c.MontoPagado).ToString("C2")
        });
        var filasPagos = pagos.Select(p => new[]
        {
            p.FechaUtc.ToLocalTime().ToString("dd/MM/yyyy"),
            WebUtility.HtmlEncode(p.FormaPago),
            WebUtility.HtmlEncode(p.Referencia ?? "—"),
            p.Monto.ToString("C2")
        });

        var cuerpo = new StringBuilder()
            .Append("<p>Estimado(a) <strong>").Append(WebUtility.HtmlEncode(prov.Nombre)).Append("</strong> (RFC ")
            .Append(WebUtility.HtmlEncode(prov.Rfc)).Append("):</p>")
            .Append("<p>Estado de cuenta de nuestras compras a usted al ").Append(DateTime.Now.ToString("dd/MM/yyyy")).Append(".</p>")
            .Append(TablaHtml(["Folio O/C", "Folio prov.", "Fecha", "Total", "Pagado", "Saldo"], filas))
            .Append("<h4 style=\"margin:12px 0 4px;\">Pagos registrados</h4>")
            .Append(pagos.Count > 0 ? TablaHtml(["Fecha", "Forma pago", "Referencia", "Monto"], filasPagos)
                : "<p><em>Sin pagos registrados.</em></p>")
            .Append("<p style=\"text-align:right;margin-top:12px;\">Saldo total: <strong style=\"font-size:16px;\">")
            .Append(saldo.ToString("C2")).Append("</strong></p>");

        var mensaje = new EmailMensajeDto(destino,
            $"Estado de cuenta (CxP) — {empresa}",
            PlantillaHtml("Estado de cuenta — Cuentas por pagar", cuerpo.ToString()), prov.Nombre);
        await EnviarAsync(mensaje, ct);
        return new EnvioCorreoResult(true, $"Estado de cuenta enviado a {destino} (saldo {saldo:C2}).");
    }
}


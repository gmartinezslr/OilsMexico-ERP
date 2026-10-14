using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QRCoder;
using System.Drawing;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Exportación Excel/PDF (checklist §2.1). Genera .xlsx (ClosedXML) y .pdf (QuestPDF)
/// en memoria para descarga directa. Todo respeta REGLA #1 (sucursal).
/// División: este archivo = helpers + reportes Excel; `.Pdf.cs` = PDFs; `.Consultas.cs` = datos.
/// </summary>
public sealed partial class ExportacionService(
    ErpDbContext db, ISucursalContext ctx, IReportesFinancierosService Reportes) : IExportacionService
{
    private const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string MimePdf = "application/pdf";

    /// <summary>Marca un libro Excel con el estilo de encabezado y auto-ajusta columnas.</summary>
    private static XLWorkbook NuevoLibro(string titulo, string[] encabezados, Action<IXLWorksheet> llenar)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Reporte");
        ws.Cell(1, 1).Value = titulo;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        for (int c = 0; c < encabezados.Length; c++)
        {
            ws.Cell(3, c + 1).Value = encabezados[c];
            ws.Cell(3, c + 1).Style.Font.Bold = true;
            ws.Cell(3, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
        }
        llenar(ws);
        ws.Columns().AdjustToContents();
        return wb;
    }

    private static byte[] ABytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string NombreConFecha(string baseNombre, string ext) =>
        $"{baseNombre}_{DateTime.Now:yyyyMMdd_HHmm}.{ext}";
    // =========================================================================
    // REPORTES EXCEL
    // =========================================================================

    /// <summary>Estado de cuenta de un cliente (facturas + cobros + saldo).</summary>
    public async Task<ArchivoExportadoDto> EstadoCuentaClienteAsync(int clienteId, CancellationToken ct = default)
    {
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteId, ct)
            ?? throw new InvalidOperationException("Cliente no encontrado.");
        var suc = ctx.ResolverSucursalAutorizada(0);

        var facturas = await db.Facturas.AsNoTracking()
            .Where(f => f.ClienteId == clienteId && f.SucursalId == suc
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .OrderByDescending(f => f.FechaEmision)
            .Select(f => new { f.FolioInterno, f.FechaEmision, f.Total, f.UuidSat })
            .ToListAsync(ct);
        var cobros = await db.VentaCobros.AsNoTracking()
            .Where(v => v.ClienteId == clienteId && v.SucursalId == suc
                && (v.Factura == null || (v.Factura.Estado != EstadoFactura.Cancelada && v.Factura.Estado != EstadoFactura.Devolucion)))
            .OrderByDescending(v => v.FechaPagoUtc)
            .Select(v => new { v.FechaPagoUtc, v.Monto, v.Referencia })
            .ToListAsync(ct);

        var totalFac = facturas.Sum(f => f.Total);
        var totalCob = cobros.Sum(v => v.Monto);

        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("EstadoCuenta");
        ws.Cell(1, 1).Value = $"Estado de cuenta — {cliente.Nombre} ({cliente.Rfc})";
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(3, 1).Value = "FACTURAS"; ws.Cell(3, 1).Style.Font.Bold = true;
        var enc1 = new[] { "Folio", "Fecha", "Total", "UUID" };
        for (int i = 0; i < enc1.Length; i++) { ws.Cell(4, i + 1).Value = enc1[i]; ws.Cell(4, i + 1).Style.Font.Bold = true; }
        int row = 5;
        foreach (var f in facturas)
        {
            ws.Cell(row, 1).Value = f.FolioInterno;
            ws.Cell(row, 2).Value = f.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy");
            ws.Cell(row, 3).Value = f.Total; ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 4).Value = f.UuidSat?.ToString() ?? "";
            row++;
        }
        row++;
        ws.Cell(row, 1).Value = "COBROS"; ws.Cell(row, 1).Style.Font.Bold = true; row++;
        var enc2 = new[] { "Fecha", "Monto", "Referencia" };
        for (int i = 0; i < enc2.Length; i++) { ws.Cell(row, i + 1).Value = enc2[i]; ws.Cell(row, i + 1).Style.Font.Bold = true; }
        row++;
        foreach (var v in cobros)
        {
            ws.Cell(row, 1).Value = v.FechaPagoUtc.ToLocalTime().ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = v.Monto; ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 3).Value = v.Referencia ?? "";
            row++;
        }
        row++;
        ws.Cell(row, 1).Value = "Total facturas"; ws.Cell(row, 3).Value = totalFac;
        ws.Cell(row + 1, 1).Value = "Total cobrado"; ws.Cell(row + 1, 3).Value = totalCob;
        ws.Cell(row + 2, 1).Value = "Saldo"; ws.Cell(row + 2, 3).Value = Math.Round(totalFac - totalCob, 2);
        ws.Cell(row + 2, 1).Style.Font.Bold = true; ws.Cell(row + 2, 3).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        return new ArchivoExportadoDto(NombreConFecha($"EstadoCuenta_{cliente.Nombre}", "xlsx"), MimeXlsx, ABytes(wb));
    }
    /// <summary>Cuentas por cobrar pendientes de la sucursal.</summary>
    public async Task<ArchivoExportadoDto> CxPendientesAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var filas = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == suc && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .Select(f => new { f.ClienteId, f.Total })
            .GroupBy(x => x.ClienteId)
            .Select(g => new { ClienteId = g.Key, Total = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        // Cobrado por cliente (cash-basis: ignora cobros de facturas canceladas/devueltas).
        var cobros = await db.VentaCobros.AsNoTracking()
            .Where(v => v.SucursalId == suc
                && (v.Factura == null || (v.Factura.Estado != EstadoFactura.Cancelada && v.Factura.Estado != EstadoFactura.Devolucion)))
            .GroupBy(v => v.ClienteId)
            .Select(g => new { ClienteId = g.Key, Cobrado = g.Sum(v => v.Monto) })
            .ToDictionaryAsync(x => x.ClienteId, x => x.Cobrado, ct);
        var nombres = await db.Clientes.AsNoTracking().Where(c => filas.Select(f => f.ClienteId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);

        var wb = NuevoLibro("Cuentas por cobrar pendientes", new[] { "Cliente", "Total", "Cobrado", "Saldo" }, ws =>
        {
            int row = 4;
            foreach (var f in filas
                .Select(f => new { f.ClienteId, f.Total, Cobrado = cobros.GetValueOrDefault(f.ClienteId, 0m) })
                .Where(f => f.Total - f.Cobrado > 0.01m)
                .OrderByDescending(f => f.Total - f.Cobrado))
            {
                ws.Cell(row, 1).Value = nombres.GetValueOrDefault(f.ClienteId, $"#{f.ClienteId}");
                ws.Cell(row, 2).Value = f.Total;
                ws.Cell(row, 3).Value = f.Cobrado;
                ws.Cell(row, 4).Value = Math.Round(f.Total - f.Cobrado, 2);
                row++;
            }
        });
        return new ArchivoExportadoDto(NombreConFecha("CxPendientes", "xlsx"), MimeXlsx, ABytes(wb));
    }

    /// <summary>Cuentas por pagar pendientes de la sucursal.</summary>
    public async Task<ArchivoExportadoDto> CxPPendientesAsync(int sucursalId, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var compras = await db.Compras.AsNoTracking()
            .Where(c => c.SucursalId == suc && c.Estado != EstadoCompra.Cancelada && c.EstadoPago != EstadoPagoCompra.Pagada)
            .Select(c => new { c.FolioInterno, c.FechaEmision, Proveedor = c.Proveedor != null ? c.Proveedor.Nombre : "", c.Total, c.MontoPagado, c.EstadoPago })
            .OrderBy(c => c.FechaEmision).ToListAsync(ct);

        var wb = NuevoLibro("Cuentas por pagar pendientes", new[] { "Folio", "Fecha", "Proveedor", "Total", "Pagado", "Saldo", "Estado" }, ws =>
        {
            int row = 4;
            foreach (var c in compras)
            {
                ws.Cell(row, 1).Value = c.FolioInterno;
                ws.Cell(row, 2).Value = c.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = c.Proveedor;
                ws.Cell(row, 4).Value = c.Total;
                ws.Cell(row, 5).Value = c.MontoPagado;
                ws.Cell(row, 6).Value = Math.Round(c.Total - c.MontoPagado, 2);
                ws.Cell(row, 7).Value = c.EstadoPago.ToString();
                row++;
            }
        });
        return new ArchivoExportadoDto(NombreConFecha("CxPPendientes", "xlsx"), MimeXlsx, ABytes(wb));
    }
    /// <summary>Inventario (stock por sucursal).</summary>
    public async Task<ArchivoExportadoDto> InventarioAsync(int sucursalId, string? filtro = null, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var q = db.InventarioSucursal.AsNoTracking().Where(i => i.SucursalId == suc);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var t = filtro.Trim().ToLower();
            q = q.Where(i => i.Producto!.Sku.ToLower().Contains(t) || i.Producto.Nombre.ToLower().Contains(t) || i.Producto.Marca.ToLower().Contains(t));
        }
        var filas = await q
            .Select(i => new { i.Producto!.Sku, i.Producto.Nombre, i.Producto.Marca, i.Producto.Viscosidad, i.StockActual, i.StockMinimo, i.Producto.PrecioVenta })
            .OrderBy(x => x.Nombre).ToListAsync(ct);

        var wb = NuevoLibro("Inventario", new[] { "SKU", "Producto", "Marca", "Viscosidad", "Stock", "Mínimo", "Valor" }, ws =>
        {
            int row = 4;
            foreach (var f in filas)
            {
                ws.Cell(row, 1).Value = f.Sku;
                ws.Cell(row, 2).Value = f.Nombre;
                ws.Cell(row, 3).Value = f.Marca;
                ws.Cell(row, 4).Value = f.Viscosidad;
                ws.Cell(row, 5).Value = f.StockActual;
                ws.Cell(row, 6).Value = f.StockMinimo;
                ws.Cell(row, 7).Value = Math.Round(f.StockActual * f.PrecioVenta, 2);
                row++;
            }
        });
        return new ArchivoExportadoDto(NombreConFecha("Inventario", "xlsx"), MimeXlsx, ABytes(wb));
    }

    /// <summary>Kardex de un producto (movimientos).</summary>
    public async Task<ArchivoExportadoDto> KardexAsync(int sucursalId, int productoId, int dias = 30, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var desde = DateTime.UtcNow.AddDays(-dias);
        var producto = await db.Productos.AsNoTracking().Where(p => p.Id == productoId).Select(p => new { p.Sku, p.Nombre }).FirstOrDefaultAsync(ct);
        var movs = await db.Movimientos.AsNoTracking()
            .Where(m => m.SucursalId == suc && m.ProductoId == productoId && m.FechaUtc >= desde)
            .OrderByDescending(m => m.FechaUtc)
            .Select(m => new { m.FechaUtc, m.Tipo, m.CantidadLitros, m.Motivo, m.UsuarioId })
            .ToListAsync(ct);

        var wb = NuevoLibro($"Kardex — {producto?.Nombre ?? productoId.ToString()} ({producto?.Sku})", new[] { "Fecha", "Tipo", "Litros", "Motivo", "Usuario" }, ws =>
        {
            int row = 4;
            foreach (var m in movs)
            {
                ws.Cell(row, 1).Value = m.FechaUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 2).Value = m.Tipo;
                ws.Cell(row, 3).Value = m.CantidadLitros;
                ws.Cell(row, 4).Value = m.Motivo ?? "";
                ws.Cell(row, 5).Value = m.UsuarioId;
                row++;
            }
        });
        return new ArchivoExportadoDto(NombreConFecha($"Kardex_{producto?.Sku ?? productoId.ToString()}", "xlsx"), MimeXlsx, ABytes(wb));
    }
    /// <summary>Ventas del periodo (historial completo sin paginar).</summary>
    public async Task<ArchivoExportadoDto> VentasAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var ini = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var fin = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var facturas = await db.Facturas.AsNoTracking()
            .Where(f => f.SucursalId == suc && f.FechaEmision >= ini && f.FechaEmision < fin
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .OrderByDescending(f => f.FechaEmision)
            .Select(f => new { f.FolioInterno, f.FechaEmision, Cliente = f.Cliente != null ? f.Cliente.Nombre : "", f.Subtotal, f.Iva, f.Total, f.Estado })
            .ToListAsync(ct);

        var wb = NuevoLibro($"Ventas {desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}", new[] { "Folio", "Fecha", "Cliente", "Subtotal", "IVA", "Total", "Estado" }, ws =>
        {
            int row = 4;
            foreach (var f in facturas)
            {
                ws.Cell(row, 1).Value = f.FolioInterno;
                ws.Cell(row, 2).Value = f.FechaEmision.ToLocalTime().ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = f.Cliente;
                ws.Cell(row, 4).Value = f.Subtotal;
                ws.Cell(row, 5).Value = f.Iva;
                ws.Cell(row, 6).Value = f.Total;
                ws.Cell(row, 7).Value = f.Estado.ToString();
                row++;
            }
            row++;
            ws.Cell(row, 1).Value = "TOTAL"; ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = facturas.Sum(f => f.Total); ws.Cell(row, 6).Style.Font.Bold = true;
        });
        return new ArchivoExportadoDto(NombreConFecha("Ventas", "xlsx"), MimeXlsx, ABytes(wb));
    }

    /// <summary>Lista de precios del catálogo activo.</summary>
    public async Task<ArchivoExportadoDto> ListaPreciosAsync(CancellationToken ct = default)
    {
        var productos = await db.Productos.AsNoTracking().Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new { p.Sku, p.Nombre, p.Marca, p.Viscosidad, p.PrecioLista, p.PrecioUnitario, p.PrecioVenta, p.PrecioMayoreo })
            .ToListAsync(ct);

        var wb = NuevoLibro("Lista de precios", new[] { "SKU", "Producto", "Marca", "Viscosidad", "P. Lista", "P. Unitario", "P. Venta", "P. Mayoreo" }, ws =>
        {
            int row = 4;
            foreach (var p in productos)
            {
                ws.Cell(row, 1).Value = p.Sku;
                ws.Cell(row, 2).Value = p.Nombre;
                ws.Cell(row, 3).Value = p.Marca;
                ws.Cell(row, 4).Value = p.Viscosidad;
                ws.Cell(row, 5).Value = p.PrecioLista;
                ws.Cell(row, 6).Value = p.PrecioUnitario;
                ws.Cell(row, 7).Value = p.PrecioVenta;
                ws.Cell(row, 8).Value = p.PrecioMayoreo;
                row++;
            }
        });
        return new ArchivoExportadoDto(NombreConFecha("ListaPrecios", "xlsx"), MimeXlsx, ABytes(wb));
    }
    /// <summary>Gastos operativos del periodo por sucursal.</summary>
    public async Task<ArchivoExportadoDto> GastosAsync(int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = ctx.ResolverSucursalAutorizada(sucursalId);
        var ini = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var fin = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
        var gastos = await db.Gastos.AsNoTracking()
            .Where(g => g.SucursalId == suc && g.Fecha >= ini && g.Fecha < fin)
            .OrderByDescending(g => g.Fecha)
            .Select(g => new { g.Fecha, g.Categoria, g.Concepto, g.Importe, g.FormaPagoSat, g.Comprobante })
            .ToListAsync(ct);

        var wb = NuevoLibro($"Gastos {desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}", new[] { "Fecha", "Categoría", "Concepto", "Importe", "Forma pago", "Comprobante" }, ws =>
        {
            int row = 4;
            foreach (var g in gastos)
            {
                ws.Cell(row, 1).Value = g.Fecha.ToLocalTime().ToString("dd/MM/yyyy");
                ws.Cell(row, 2).Value = g.Categoria;
                ws.Cell(row, 3).Value = g.Concepto;
                ws.Cell(row, 4).Value = g.Importe;
                ws.Cell(row, 5).Value = g.FormaPagoSat;
                ws.Cell(row, 6).Value = g.Comprobante ?? "";
                row++;
            }
            row++;
            ws.Cell(row, 3).Value = "TOTAL"; ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = gastos.Sum(g => g.Importe); ws.Cell(row, 4).Style.Font.Bold = true;
        });
        return new ArchivoExportadoDto(NombreConFecha("Gastos", "xlsx"), MimeXlsx, ABytes(wb));
    }

    /// <summary>Comisiones: ventas atribuidas del periodo por vendedor (base de cálculo).</summary>
    public async Task<ArchivoExportadoDto> ComisionesAsync(int? sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var suc = sucursalId.HasValue ? ctx.ResolverSucursalAutorizada(sucursalId.Value) : 0;
        var ini = DateTime.SpecifyKind(desde.Date, DateTimeKind.Local).ToUniversalTime();
        var fin = DateTime.SpecifyKind(hasta.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var filas = await db.Facturas.AsNoTracking()
            .Where(f => (suc == 0 || f.SucursalId == suc)
                && f.FechaEmision >= ini && f.FechaEmision < fin
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
            .GroupBy(f => f.VendedorId)
            .Select(g => new { VendedorId = g.Key, Total = g.Sum(f => f.Total), Facturas = g.Count() })
            .ToListAsync(ct);
        var ids = filas.Where(f => f.VendedorId != null).Select(f => f.VendedorId!.Value).ToList();
        var nombres = await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        var wb = NuevoLibro($"Comisiones (ventas) {desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}", new[] { "Vendedor", "# Ventas", "Monto" }, ws =>
        {
            int row = 4;
            foreach (var f in filas.OrderByDescending(f => f.Total))
            {
                ws.Cell(row, 1).Value = f.VendedorId == null ? "(sin atribución)" : nombres.GetValueOrDefault(f.VendedorId.Value, $"#{f.VendedorId}");
                ws.Cell(row, 2).Value = f.Facturas;
                ws.Cell(row, 3).Value = f.Total;
                row++;
            }
        });
        return new ArchivoExportadoDto(NombreConFecha("Comisiones", "xlsx"), MimeXlsx, ABytes(wb));
    }
    // =========================================================================
    // REPORTES PDF (QuestPDF)
    // =========================================================================

    private static byte[] Pdf(Action<PageDescriptor> page) =>
        Document.Create(doc => doc.Page(p => { p.Margin(30); page(p); })).GeneratePdf();

    /// <summary>Tabla genérica: encabezados + filas de strings ya formateados.</summary>
    private static void TablaPdf(ColumnDescriptor col, string[] encabezados, List<string[]> filas, string titulo)
    {
        col.Item().Text(titulo).Bold().FontSize(14);
        col.Item().PaddingTop(6).Table(t =>
        {
            // Columnas relativas (proporcionales): se ajustan al ancho de página sin desbordarse,
            // lo que evita DocumentLayoutException en tablas anchas (existencias = 8 columnas).
            t.ColumnsDefinition(c =>
            {
                for (int i = 0; i < encabezados.Length; i++) c.RelativeColumn();
            });
            t.Header(h =>
            {
                foreach (var e in encabezados) h.Cell().Background("#E9ECEF").Padding(3).Text(e).Bold().FontSize(8);
            });
            foreach (var f in filas)
            {
                for (int i = 0; i < f.Length; i++)
                    t.Cell().BorderBottom(0.5f).Padding(3).Text(f[i]).FontSize(8);
            }
        });
    }

    /// <summary>PDF de un estado financiero (balanza, balance, resultados, existencias).</summary>
    public async Task<ArchivoExportadoDto> FinancieroPdfAsync(string reporte, int sucursalId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var filas = new List<string[]>();
        string titulo = reporte.ToLower() switch
        {
            "balanza" => "Balanza de Comprobación",
            "balance" => "Balance General",
            "resultados" => "Estado de Resultados",
            "existencias" => "Cambio de Existencias",
            _ => "Reporte Financiero"
        };
        string[] enc;

        if (reporte.ToLower() == "balanza")
        {
            var b = await Reportes.BalanzaAsync(sucursalId, desde, hasta, ct);
            enc = new[] { "Código", "Cuenta", "Débitos", "Créditos", "Deudor", "Acreedor" };
            foreach (var r in b.Renglones)
                filas.Add(new[] { r.Codigo, r.Nombre, r.Debitos.ToString("N2"), r.Creditos.ToString("N2"), r.SaldoDeudor.ToString("N2"), r.SaldoAcreedor.ToString("N2") });
        }
        else if (reporte.ToLower() == "balance")
        {
            var b = await Reportes.BalanceAsync(sucursalId, hasta, ct);
            enc = new[] { "Código", "Cuenta", "Saldo" };
            foreach (var sec in new[] { b.Activo, b.Pasivo, b.Patrimonio })
                foreach (var c in sec.Cuentas)
                    filas.Add(new[] { sec.Titulo, $"{c.Codigo} {c.Nombre}", c.Saldo.ToString("N2") });
        }
        else if (reporte.ToLower() == "resultados")
        {
            var b = await Reportes.ResultadosAsync(sucursalId, desde, hasta, ct);
            enc = new[] { "Concepto", "Monto" };
            filas.Add(new[] { "Ventas netas", b.VentasNetas.ToString("N2") });
            filas.Add(new[] { "Costo de ventas", b.CostoVentas.ToString("N2") });
            filas.Add(new[] { "Utilidad bruta", b.UtilidadBruta.ToString("N2") });
            filas.Add(new[] { "Utilidad neta", b.UtilidadNeta.ToString("N2") });
            filas.Add(new[] { "Margen bruto %", b.MargenBrutoPct.ToString("N2") });
        }
        else
        {
            var b = await Reportes.ExistenciasAsync(sucursalId, desde, hasta, ct);
            enc = new[] { "SKU", "Producto", "Inicial", "Compras", "Ventas", "Ajustes", "Traspasos", "Final" };
            foreach (var r in b.Renglones)
                filas.Add(new[] { r.Sku, r.Nombre, r.Inicial.ToString("N2"), r.Compras.ToString("N2"), r.Ventas.ToString("N2"), r.Ajustes.ToString("N2"), r.Traspasos.ToString("N2"), r.Final.ToString("N2") });
        }

        var pdf = Pdf(p =>
        {
            p.Header().Text($"OilsMexico ERP — {titulo}").Bold().FontSize(16);
            p.Content().PaddingTop(10).Column(col => TablaPdf(col, enc, filas, $"Periodo {desde:dd/MM/yyyy} — {hasta:dd/MM/yyyy}"));
        });
        return new ArchivoExportadoDto(NombreConFecha(titulo.Replace(" ", ""), "pdf"), MimePdf, pdf);
    }
    /// <summary>PDF de factura CFDI con QR SAT (representación impresa formal).</summary>
    public async Task<ArchivoExportadoDto> FacturaCfdiPdfAsync(int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas.AsNoTracking()
            .Where(x => x.Id == facturaId)
            .Select(x => new
            {
                x.Id, x.FolioInterno, x.FechaEmision, x.Subtotal, x.Iva, x.Total,
                x.UuidSat, x.FormaPagoSat, x.MetodoPagoSat,
                EmisorRfc = x.Sucursal != null ? x.Sucursal.RfcEmisor : "",
                EmisorNombre = x.Sucursal != null ? x.Sucursal.RazonSocial ?? x.Sucursal.Nombre : "",
                Receptor = x.Cliente != null ? x.Cliente.Nombre : "",
                ReceptorRfc = x.Cliente != null ? x.Cliente.Rfc : "",
                Renglones = x.Detalles.Select(d => new { d.Producto!.Nombre, d.UnidadNombre, d.Cantidad, d.PrecioUnitario, d.Importe }).ToList()
            })
            .FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Factura no encontrada.");

        // URL de verificación SAT para el QR (formato oficial).
        var qrTexto = $"https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id={f.UuidSat}&re={f.EmisorRfc}&rr={f.ReceptorRfc}&tt={f.Total:0.00}";
        byte[] qrPng = GenerarQr(qrTexto);

        var pdf = Pdf(p =>
        {
            p.Header().Row(h =>
            {
                h.RelativeItem().Text($"Factura {f.FolioInterno}").Bold().FontSize(16);
                h.ConstantItem(90).Image(qrPng);
            });
            p.Content().PaddingTop(8).Column(col =>
            {
                col.Item().Text($"Emisor: {f.EmisorNombre} ({f.EmisorRfc})");
                col.Item().Text($"Receptor: {f.Receptor} ({f.ReceptorRfc})");
                col.Item().Text($"Fecha: {f.FechaEmision.ToLocalTime():dd/MM/yyyy HH:mm} · UUID: {f.UuidSat}");
                col.Item().Text($"Forma pago: {f.FormaPagoSat} · Método: {f.MetodoPagoSat}");
                col.Item().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    t.Header(h =>
                    {
                        h.Cell().Text("Producto").Bold();
                        h.Cell().Text("Cant").Bold();
                        h.Cell().Text("P.Unit").Bold();
                        h.Cell().Text("Importe").Bold();
                    });
                    foreach (var r in f.Renglones)
                    {
                        t.Cell().Text(r.Nombre).FontSize(9);
                        t.Cell().Text($"{r.Cantidad} {r.UnidadNombre}").FontSize(9);
                        t.Cell().Text(r.PrecioUnitario.ToString("N2")).FontSize(9);
                        t.Cell().Text(r.Importe.ToString("N2")).FontSize(9);
                    }
                });
                col.Item().PaddingTop(6).AlignRight().Text($"Subtotal: {f.Subtotal:N2} · IVA: {f.Iva:N2} · Total: {f.Total:N2}").Bold();
            });
        });
        return new ArchivoExportadoDto(NombreConFecha($"Factura_{f.FolioInterno}", "pdf"), MimePdf, pdf);
    }

    private static byte[] GenerarQr(string texto)
    {
        // Mismo patrón que AuthService.GenerarQrImagenBase64 (QRCoder, GetGraphic → Bitmap).
        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(texto, QRCodeGenerator.ECCLevel.Q);
        using var code = new QRCoder.QRCode(data);
        using var bitmap = code.GetGraphic(16);
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
    /// <summary>PDF de cotización (renglones + totales + vigencia).</summary>
    public async Task<ArchivoExportadoDto> CotizacionPdfAsync(int cotizacionId, CancellationToken ct = default)
    {
        var c = await db.Cotizaciones.AsNoTracking()
            .Where(x => x.Id == cotizacionId)
            .Select(x => new
            {
                x.Id, x.FolioInterno, x.FechaEmision, x.FechaVencimiento, x.Subtotal, x.Iva, x.Total, x.Estado,
                Cliente = x.Cliente != null ? x.Cliente.Nombre : "",
                Renglones = x.Detalles.Select(d => new { d.Producto!.Nombre, d.UnidadNombre, d.Cantidad, d.PrecioUnitario, d.Importe }).ToList()
            })
            .FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Cotización no encontrada.");

        var pdf = Pdf(p =>
        {
            p.Header().Text($"Cotización {c.FolioInterno}").Bold().FontSize(16);
            p.Content().PaddingTop(8).Column(col =>
            {
                col.Item().Text($"Cliente: {c.Cliente}");
                col.Item().Text($"Emisión: {c.FechaEmision.ToLocalTime():dd/MM/yyyy} · Vigencia: {(c.FechaVencimiento?.ToLocalTime().ToString("dd/MM/yyyy") ?? "sin vigencia")} · Estado: {c.Estado}");
                col.Item().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                    t.Header(h => { h.Cell().Text("Producto").Bold(); h.Cell().Text("Cant").Bold(); h.Cell().Text("P.Unit").Bold(); h.Cell().Text("Importe").Bold(); });
                    foreach (var r in c.Renglones)
                    {
                        t.Cell().Text(r.Nombre).FontSize(9);
                        t.Cell().Text($"{r.Cantidad} {r.UnidadNombre}").FontSize(9);
                        t.Cell().Text(r.PrecioUnitario.ToString("N2")).FontSize(9);
                        t.Cell().Text(r.Importe.ToString("N2")).FontSize(9);
                    }
                });
                col.Item().PaddingTop(6).AlignRight().Text($"Subtotal: {c.Subtotal:N2} · IVA: {c.Iva:N2} · Total: {c.Total:N2}").Bold();
            });
        });
        return new ArchivoExportadoDto(NombreConFecha($"Cotizacion_{c.FolioInterno}", "pdf"), MimePdf, pdf);
    }

    /// <summary>PDF de orden de venta (renglones + totales).</summary>
    public async Task<ArchivoExportadoDto> OrdenVentaPdfAsync(int ordenId, CancellationToken ct = default)
    {
        var o = await db.OrdenesVenta.AsNoTracking()
            .Where(x => x.Id == ordenId)
            .Select(x => new
            {
                x.Id, x.FolioInterno, x.FechaEmision, x.Subtotal, x.Iva, x.Total, x.Estado,
                Cliente = x.Cliente != null ? x.Cliente.Nombre : "",
                Renglones = x.Detalles.Select(d => new { d.Producto!.Nombre, d.UnidadNombre, d.Cantidad, d.PrecioUnitario, d.Importe }).ToList()
            })
            .FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("Orden de venta no encontrada.");

        var pdf = Pdf(p =>
        {
            p.Header().Text($"Orden de venta {o.FolioInterno}").Bold().FontSize(16);
            p.Content().PaddingTop(8).Column(col =>
            {
                col.Item().Text($"Cliente: {o.Cliente}");
                col.Item().Text($"Emisión: {o.FechaEmision.ToLocalTime():dd/MM/yyyy} · Estado: {o.Estado}");
                col.Item().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
                    t.Header(h => { h.Cell().Text("Producto").Bold(); h.Cell().Text("Cant").Bold(); h.Cell().Text("P.Unit").Bold(); h.Cell().Text("Importe").Bold(); });
                    foreach (var r in o.Renglones)
                    {
                        t.Cell().Text(r.Nombre).FontSize(9);
                        t.Cell().Text($"{r.Cantidad} {r.UnidadNombre}").FontSize(9);
                        t.Cell().Text(r.PrecioUnitario.ToString("N2")).FontSize(9);
                        t.Cell().Text(r.Importe.ToString("N2")).FontSize(9);
                    }
                });
                col.Item().PaddingTop(6).AlignRight().Text($"Subtotal: {o.Subtotal:N2} · IVA: {o.Iva:N2} · Total: {o.Total:N2}").Bold();
            });
        });
        return new ArchivoExportadoDto(NombreConFecha($"OrdenVenta_{o.FolioInterno}", "pdf"), MimePdf, pdf);
    }
}

using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// REGLA #2 — Sellado nativo CFDI 4.0 (I/E/P):
/// construye el CFDI completo (Emisor/Receptor/Conceptos/Impuestos [+Pagos 2.0]),
/// arma la cadena original con el XSLT oficial del SAT y firma RSA-SHA256 con
/// System.Security.Cryptography. Sin librerías de terceros.
/// Sin CSD y con Cfdi:PacModo=SIMULADO conserva el sello DEV auditable.
/// </summary>
public sealed class CfdiSelladoService(ErpDbContext db, IConfiguration cfg) : ICfdiSelladoService
{
    private const decimal TasaIva = 0.16m;
    private static readonly XNamespace NsCfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace NsPagos = "http://www.sat.gob.mx/Pagos20";
    private static readonly Lazy<XslCompiledTransform> CadenaXslt = new(CargarXslt);

    public async Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarAsync(
        int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cliente).Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");

        var modo = cfg["Cfdi:PacModo"] ?? "SIMULADO";

        var cerPath = cfg["Cfdi:CerPath"] ?? "certs/csd.cer";
        var keyPath = cfg["Cfdi:KeyPath"] ?? "certs/csd.key";
        var password = cfg["Cfdi:KeyPassword"] ?? string.Empty;

        if (!File.Exists(cerPath) || !File.Exists(keyPath))
        {
            if (!string.Equals(modo, "SIMULADO", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Cfdi:PacModo={modo} requiere el CSD: no se encontró '{cerPath}' o '{keyPath}'. " +
                    "Coloca el .cer y el .key en la carpeta certs/ (nunca se sube al repositorio).");

            // Modo desarrollo sin CSD: genera cadena + sello simulado auditable.
            var cadenaDev = $"||4.0|{f.FolioInterno}|{f.FechaEmision:yyyy-MM-ddTHH:mm:ss}|" +
                $"{f.FormaPagoSat}|{f.Subtotal:N2}|{f.Total:N2}|{f.Sucursal?.RfcEmisor}|{f.Cliente?.Rfc}|DEV|";
            var selloDev = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(cadenaDev + password)));
            var xmlDev = $"<cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" " +
                $"Version=\"4.0\" Folio=\"{f.FolioInterno}\" Sello=\"{selloDev}\" />";
            f.CadenaOriginal = cadenaDev; f.SelloDigital = selloDev; f.XmlSellado = xmlDev;
            await db.SaveChangesAsync(ct);
            return (cadenaDev, selloDev, xmlDev);
        }

        // ================= CFDI 4.0 real (sellado con CSD) =================
        using var cert = X509CertificateLoader.LoadCertificateFromFile(cerPath);
        if (cert.NotAfter.ToUniversalTime() < DateTime.UtcNow)
            throw new InvalidOperationException(
                $"El CSD '{cerPath}' está vencido desde {cert.NotAfter:yyyy-MM-dd}. Renueva el CSD antes de timbrar.");
        using var rsa = CargarLlavePrivada(keyPath, password);

        var suc = f.Sucursal
            ?? throw new InvalidOperationException("La factura no tiene sucursal (emisor).");
        var emisor = ResolverEmisor(suc, f.Cliente);

        if (f.Detalles.Count == 0)
            throw new InvalidOperationException("La factura no tiene renglones; no es posible generar el CFDI.");

        // Los precios del POS incluyen IVA; el CFDI exige importes netos por renglón.
        // Cuadre de redondeos: ΣConIva = Total y ΣNeto = SubTotal ⇒ SubTotal + TotalImpuestos = Total.
        var lineas = f.Detalles
            .Where(d => d.Cantidad != 0 || d.Importe != 0)
            .Select(d => new LineaCfdi
            {
                Cantidad = d.Cantidad,
                ConIva = Math.Round(d.Importe, 2),
                Descripcion = d.Producto is null
                    ? $"Producto {d.ProductoId}"
                    : $"{d.Producto.Nombre} {d.Producto.Viscosidad}".Trim(),
                Sku = d.Producto?.Sku,
                Unidad = string.IsNullOrWhiteSpace(d.UnidadNombre) ? "Pieza" : d.UnidadNombre.Trim()
            })
            .ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("La factura no tiene renglones con importe; no es posible generar el CFDI.");

        NormalizarLineas(lineas, f.Subtotal, f.Total);
        var totalImpuestos = Math.Round(lineas.Sum(l => l.ConIva - l.Neto), 2);

        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(f.FechaEmision, ZonaMexico())
            .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        var conceptos = ArmarConceptos(lineas);

        var comprobante = new XElement(NsCfdi + "Comprobante",
            new XAttribute("Version", "4.0"),
            new XAttribute("Folio", f.FolioInterno),
            new XAttribute("Fecha", fechaLocal),
            new XAttribute("FormaPago", f.FormaPagoSat),
            new XAttribute("MetodoPago", f.MetodoPagoSat),
            new XAttribute("SubTotal", Formato(f.Subtotal)),
            new XAttribute("Moneda", "MXN"),
            new XAttribute("Total", Formato(f.Total)),
            new XAttribute("TipoDeComprobante", "I"),
            new XAttribute("Exportacion", "01"),
            new XAttribute("LugarExpedicion", emisor.Cp),
            new XAttribute("NoCertificado", NumeroCertificado(cert)),
            new XAttribute("Certificado", Convert.ToBase64String(cert.GetRawCertData())),
            new XElement(NsCfdi + "Emisor",
                new XAttribute("Rfc", emisor.Rfc),
                new XAttribute("Nombre", emisor.RazonSocial),
                new XAttribute("RegimenFiscal", emisor.Regimen)),
            new XElement(NsCfdi + "Receptor",
                new XAttribute("Rfc", emisor.ReceptorRfc),
                new XAttribute("Nombre", emisor.ReceptorNombre),
                new XAttribute("DomicilioFiscalReceptor", emisor.ReceptorCp),
                new XAttribute("RegimenFiscalReceptor", emisor.ReceptorRegimen),
                new XAttribute("UsoCFDI", f.UsoCfdi)),
            conceptos);
        AgregarImpuestosGlobales(comprobante, f.Subtotal, totalImpuestos);

        // Cadena original con el XSLT oficial del SAT (lee @NoCertificado; no toca @Sello).
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), comprobante);
        var cadena = CalcularCadenaOriginal(doc);
        var sello = Convert.ToBase64String(rsa.SignData(
            Encoding.UTF8.GetBytes(cadena), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        comprobante.SetAttributeValue("Sello", sello);

        var xmlFinal = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine
            + comprobante.ToString(SaveOptions.DisableFormatting);
        f.CadenaOriginal = cadena;
        f.SelloDigital = sello;
        f.XmlSellado = xmlFinal;
        await db.SaveChangesAsync(ct);
        return (cadena, sello, xmlFinal);
    }

    public async Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarNotaCreditoAsync(
        int notaId, CancellationToken ct = default)
    {
        var nc = await db.NotasCredito
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cliente).Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == notaId, ct)
            ?? throw new InvalidOperationException("Nota de crédito no existe.");
        if (nc.UuidFacturaOrigen is null)
            throw new InvalidOperationException("La NC no tiene UUID de factura origen (CfdiRelacionados 01).");
        var modo = cfg["Cfdi:PacModo"] ?? "SIMULADO";
        var cerPath = cfg["Cfdi:CerPath"] ?? "certs/csd.cer";
        var keyPath = cfg["Cfdi:KeyPath"] ?? "certs/csd.key";
        var password = cfg["Cfdi:KeyPassword"] ?? string.Empty;
        if (!File.Exists(cerPath) || !File.Exists(keyPath))
            throw new InvalidOperationException(
                $"Cfdi:PacModo={modo} requiere el CSD para la NC: no se encontró '{cerPath}' o '{keyPath}'.");
        using var cert = X509CertificateLoader.LoadCertificateFromFile(cerPath);
        using var rsa = CargarLlavePrivada(keyPath, password);
        var emisor = ResolverEmisor(nc.Sucursal, nc.Cliente);
        var lineas = nc.Detalles
            .Where(d => d.Cantidad != 0 || d.Importe != 0)
            .Select(d => new LineaCfdi
            {
                Cantidad = d.Cantidad,
                ConIva = Math.Round(d.Importe, 2),
                Descripcion = d.Producto is null ? $"Producto {d.ProductoId}"
                    : $"{d.Producto.Nombre} {d.Producto.Viscosidad}".Trim(),
                Sku = d.Producto?.Sku,
                Unidad = string.IsNullOrWhiteSpace(d.UnidadNombre) ? "Pieza" : d.UnidadNombre.Trim()
            }).ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("La NC no tiene renglones con importe.");
        NormalizarLineas(lineas, nc.Subtotal, nc.Total);
        var totalImpuestos = Math.Round(lineas.Sum(l => l.ConIva - l.Neto), 2);
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(nc.FechaEmision, ZonaMexico())
            .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var conceptos = ArmarConceptos(lineas);
        var comprobante = new XElement(NsCfdi + "Comprobante",
            new XAttribute("Version", "4.0"),
            new XAttribute("Folio", nc.FolioInterno),
            new XAttribute("Fecha", fechaLocal),
            new XAttribute("SubTotal", Formato(nc.Subtotal)),
            new XAttribute("Moneda", "MXN"),
            new XAttribute("Total", Formato(nc.Total)),
            new XAttribute("TipoDeComprobante", "E"),
            new XAttribute("Exportacion", "01"),
            new XAttribute("LugarExpedicion", emisor.Cp),
            new XAttribute("NoCertificado", NumeroCertificado(cert)),
            new XAttribute("Certificado", Convert.ToBase64String(cert.GetRawCertData())),
            new XElement(NsCfdi + "CfdiRelacionados",
                new XAttribute("TipoRelacion", string.IsNullOrWhiteSpace(nc.TipoRelacion) ? "01" : nc.TipoRelacion.Trim()),
                new XElement(NsCfdi + "CfdiRelacionado",
                    new XAttribute("UUID", nc.UuidFacturaOrigen.Value.ToString().ToUpperInvariant()))),
            new XElement(NsCfdi + "Emisor",
                new XAttribute("Rfc", emisor.Rfc),
                new XAttribute("Nombre", emisor.RazonSocial),
                new XAttribute("RegimenFiscal", emisor.Regimen)),
            new XElement(NsCfdi + "Receptor",
                new XAttribute("Rfc", emisor.ReceptorRfc),
                new XAttribute("Nombre", emisor.ReceptorNombre),
                new XAttribute("DomicilioFiscalReceptor", emisor.ReceptorCp),
                new XAttribute("RegimenFiscalReceptor", emisor.ReceptorRegimen),
                new XAttribute("UsoCFDI", string.IsNullOrWhiteSpace(nc.UsoCfdi) ? "G02" : nc.UsoCfdi.Trim())),
            conceptos);
        AgregarImpuestosGlobales(comprobante, nc.Subtotal, totalImpuestos);
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), comprobante);
        var cadena = CalcularCadenaOriginal(doc);
        var sello = Convert.ToBase64String(rsa.SignData(
            Encoding.UTF8.GetBytes(cadena), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        comprobante.SetAttributeValue("Sello", sello);
        var xmlFinal = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine
            + comprobante.ToString(SaveOptions.DisableFormatting);
        nc.CadenaOriginal = cadena;
        nc.SelloDigital = sello;
        nc.XmlSellado = xmlFinal;
        await db.SaveChangesAsync(ct);
        return (cadena, sello, xmlFinal);
    }

    public async Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarRepAsync(
        int repId, CancellationToken ct = default)
    {
        var rep = await db.ComplementosPago
            .Include(x => x.Documentos)
            .Include(x => x.Cliente).Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == repId, ct)
            ?? throw new InvalidOperationException("Complemento de pago no existe.");
        if (rep.Documentos.Count == 0)
            throw new InvalidOperationException("El REP no tiene documentos relacionados.");
        var cerPath = cfg["Cfdi:CerPath"] ?? "certs/csd.cer";
        var keyPath = cfg["Cfdi:KeyPath"] ?? "certs/csd.key";
        var password = cfg["Cfdi:KeyPassword"] ?? string.Empty;
        if (!File.Exists(cerPath) || !File.Exists(keyPath))
            throw new InvalidOperationException(
                $"Timbrado REP requiere el CSD: no se encontró '{cerPath}' o '{keyPath}'.");
        using var cert = X509CertificateLoader.LoadCertificateFromFile(cerPath);
        using var rsa = CargarLlavePrivada(keyPath, password);
        var emisor = ResolverEmisor(rep.Sucursal, rep.Cliente);
        var montoTotal = Math.Round(rep.Documentos.Sum(d => d.ImpPagado), 2);
        if (montoTotal <= 0)
            throw new InvalidOperationException("El REP no tiene monto pagado.");
        var formaPago = await db.VentaCobros.AsNoTracking()
            .Where(c => c.ComplementoPagoId == rep.Id)
            .Select(c => c.FormaPagoSat).FirstOrDefaultAsync(ct) ?? "03";
        var fechaPago = TimeZoneInfo.ConvertTimeFromUtc(rep.FechaEmision, ZonaMexico())
            .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var pagos = new XElement(NsPagos + "Pagos", new XAttribute("Version", "2.0"),
            new XElement(NsPagos + "Totales",
                new XAttribute("TotalTrasladosBaseIVA16", Formato(0m)),
                new XAttribute("TotalTrasladosImpuestoIVA16", Formato(0m)),
                new XAttribute("MontoTotalPagos", Formato(montoTotal))),
            new XElement(NsPagos + "Pago",
                new XAttribute("FechaPago", fechaPago),
                new XAttribute("FormaDePagoP", formaPago.Trim()),
                new XAttribute("MonedaP", "MXN"),
                new XAttribute("Monto", Formato(montoTotal)),
                rep.Documentos.Select(d => new XElement(NsPagos + "DoctoRelacionado",
                    new XAttribute("IdDocumento", (d.UuidFactura ?? Guid.Empty).ToString().ToUpperInvariant()),
                    new XAttribute("Serie", SerieDeFolio(d.FolioFactura)),
                    new XAttribute("Folio", FolioDeFolio(d.FolioFactura)),
                    new XAttribute("MonedaDR", string.IsNullOrWhiteSpace(d.Moneda) ? "MXN" : d.Moneda.Trim()),
                    new XAttribute("EquivalenciaDR", "1"),
                    new XAttribute("NumParcialidad", d.NumParcialidad),
                    new XAttribute("ImpSaldoAnt", Formato(d.ImpSaldoAnt)),
                    new XAttribute("ImpPagado", Formato(d.ImpPagado)),
                    new XAttribute("ImpSaldoInsoluto", Formato(d.ImpSaldoInsoluto)),
                    new XAttribute("ObjetoImpDR", "02")))));
        var fechaLocal = TimeZoneInfo.ConvertTimeFromUtc(rep.FechaEmision, ZonaMexico())
            .ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var comprobante = new XElement(NsCfdi + "Comprobante",
            new XAttribute(XNamespace.Xmlns + "pago20", NsPagos.NamespaceName),
            new XAttribute("Version", "4.0"),
            new XAttribute("Folio", rep.FolioInterno),
            new XAttribute("Fecha", fechaLocal),
            new XAttribute("SubTotal", Formato(0m)),
            new XAttribute("Moneda", "XXX"),
            new XAttribute("Total", Formato(0m)),
            new XAttribute("TipoDeComprobante", "P"),
            new XAttribute("Exportacion", "01"),
            new XAttribute("LugarExpedicion", emisor.Cp),
            new XAttribute("NoCertificado", NumeroCertificado(cert)),
            new XAttribute("Certificado", Convert.ToBase64String(cert.GetRawCertData())),
            new XElement(NsCfdi + "Emisor",
                new XAttribute("Rfc", emisor.Rfc),
                new XAttribute("Nombre", emisor.RazonSocial),
                new XAttribute("RegimenFiscal", emisor.Regimen)),
            new XElement(NsCfdi + "Receptor",
                new XAttribute("Rfc", emisor.ReceptorRfc),
                new XAttribute("Nombre", emisor.ReceptorNombre),
                new XAttribute("DomicilioFiscalReceptor", emisor.ReceptorCp),
                new XAttribute("RegimenFiscalReceptor", emisor.ReceptorRegimen),
                new XAttribute("UsoCFDI", "CP01")),
            new XElement(NsCfdi + "Conceptos",
                new XElement(NsCfdi + "Concepto",
                    new XAttribute("ClaveProdServ", "84111506"),
                    new XAttribute("Cantidad", "1"),
                    new XAttribute("ClaveUnidad", "ACT"),
                    new XAttribute("Descripcion", "Pago"),
                    new XAttribute("ValorUnitario", Formato(0m)),
                    new XAttribute("Importe", Formato(0m)),
                    new XAttribute("ObjetoImp", "01"))),
            new XElement(NsCfdi + "Complemento", pagos));
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), comprobante);
        var cadena = CalcularCadenaOriginal(doc);
        var sello = Convert.ToBase64String(rsa.SignData(
            Encoding.UTF8.GetBytes(cadena), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        comprobante.SetAttributeValue("Sello", sello);
        var xmlFinal = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine
            + comprobante.ToString(SaveOptions.DisableFormatting);
        rep.CadenaOriginal = cadena;
        rep.SelloDigital = sello;
        rep.XmlSellado = xmlFinal;
        await db.SaveChangesAsync(ct);
        return (cadena, sello, xmlFinal);
    }

    private static string SerieDeFolio(string folio)
    {
        var t = (folio ?? string.Empty).Trim();
        var guion = t.IndexOf('-');
        return guion > 0 ? t[..guion] : "REP";
    }

    private static string FolioDeFolio(string folio)
    {
        var t = (folio ?? string.Empty).Trim();
        var guion = t.LastIndexOf('-');
        var num = guion >= 0 ? t[(guion + 1)..] : t;
        return string.IsNullOrWhiteSpace(num) ? "1" : num.Trim();
    }

    private sealed record DatosEmisor(
        string Rfc, string RazonSocial, string Regimen, string Cp,
        string ReceptorRfc, string ReceptorNombre, string ReceptorCp, string ReceptorRegimen);

    private static DatosEmisor ResolverEmisor(
        Domain.Entities.Sucursal? suc, Domain.Entities.Cliente? cli)
    {
        if (suc is null) throw new InvalidOperationException("El comprobante no tiene sucursal (emisor).");
        var rfcEmisor = (suc.RfcEmisor ?? string.Empty).Trim();
        var razonSocial = (suc.RazonSocial ?? string.Empty).Trim();
        var regimenEmisor = (suc.RegimenFiscal ?? string.Empty).Trim();
        var cpEmisor = NormalizarCp(suc.CodigoPostal)
            ?? throw new InvalidOperationException(
                "Faltan datos fiscales del emisor (RFC, razón social, régimen o CP de 5 dígitos). " +
                "Completa la página Configuración → Datos de la sucursal.");
        if (rfcEmisor.Length == 0 || razonSocial.Length == 0 || regimenEmisor.Length == 0)
            throw new InvalidOperationException(
                "Faltan datos fiscales del emisor (RFC, razón social, régimen o CP de 5 dígitos). " +
                "Completa la página Configuración → Datos de la sucursal.");
        if (cli is null) throw new InvalidOperationException("El comprobante no tiene cliente (receptor).");
        var rfcReceptor = (cli.Rfc ?? string.Empty).Trim();
        if (rfcReceptor.Length == 0)
            throw new InvalidOperationException("El cliente no tiene RFC (receptor del CFDI).");
        var regimenReceptor = (cli.RegimenFiscal ?? string.Empty).Trim();
        if (regimenReceptor.Length == 0)
            throw new InvalidOperationException(
                $"El cliente '{cli.Nombre}' no tiene régimen fiscal (Receptor@RegimenFiscalReceptor).");
        var cpReceptor = NormalizarCp(cli.CodigoPostal) ?? cpEmisor;
        return new DatosEmisor(rfcEmisor, razonSocial, regimenEmisor, cpEmisor,
            rfcReceptor, cli.Nombre.Trim(), cpReceptor, regimenReceptor);
    }

    private sealed class LineaCfdi
    {
        public decimal Cantidad { get; set; }
        public decimal ConIva { get; set; }
        public decimal Neto { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string Unidad { get; set; } = "Pieza";
    }

    private static void NormalizarLineas(List<LineaCfdi> lineas, decimal subtotal, decimal total)
    {
        if (lineas.Count == 0) return;
        // Cuadre de redondeos: ΣConIva = Total y ΣNeto = SubTotal ⇒ SubTotal + TotalImpuestos = Total.
        var difTotal = Math.Round(total - lineas.Sum(l => l.ConIva), 2);
        if (difTotal != 0)
        {
            var mayor = lineas.OrderByDescending(l => Math.Abs(l.ConIva)).First();
            mayor.ConIva += difTotal;
        }
        foreach (var l in lineas)
            l.Neto = Math.Round(l.ConIva / (1m + TasaIva), 2);
        var difSub = Math.Round(subtotal - lineas.Sum(l => l.Neto), 2);
        if (difSub != 0)
        {
            var mayor = lineas.OrderByDescending(l => Math.Abs(l.Neto)).First();
            mayor.Neto += difSub;
        }
    }

    private static string Formato(decimal valor, string formato = "0.00")
        => valor.ToString(formato, CultureInfo.InvariantCulture);

    private static XElement ArmarConceptos(List<LineaCfdi> lineas)
    {
        var conceptos = new XElement(NsCfdi + "Conceptos");
        foreach (var l in lineas)
        {
            var ivaLinea = Math.Round(l.ConIva - l.Neto, 2);
            var valorUnitario = l.Cantidad != 0 ? Math.Round(l.Neto / l.Cantidad, 6) : l.Neto;
            var concepto = new XElement(NsCfdi + "Concepto",
                new XAttribute("ClaveProdServ", "01010101"), // "No existe en el catálogo"
                new XAttribute("Cantidad", Formato(l.Cantidad, "0.######")),
                new XAttribute("ClaveUnidad", ClaveUnidadSat(l.Unidad)),
                new XAttribute("Unidad", l.Unidad),
                new XAttribute("Descripcion", l.Descripcion),
                new XAttribute("ValorUnitario", Formato(valorUnitario, "0.######")),
                new XAttribute("Importe", Formato(l.Neto)),
                new XAttribute("ObjetoImp", "02")); // objeto con impuestos
            if (!string.IsNullOrWhiteSpace(l.Sku))
            {
                var sku = l.Sku!.Trim();
                concepto.Add(new XAttribute("NoIdentificacion", sku[..Math.Min(40, sku.Length)]));
            }
            if (ivaLinea > 0)
                concepto.Add(new XElement(NsCfdi + "Impuestos",
                    new XElement(NsCfdi + "Traslados",
                        new XElement(NsCfdi + "Traslado",
                            new XAttribute("Base", Formato(l.Neto)),
                            new XAttribute("Impuesto", "002"),  // c_Impuesto: 002 = IVA
                            new XAttribute("TipoFactor", "Tasa"),
                            new XAttribute("TasaOCuota", "0.160000"),
                            new XAttribute("Importe", Formato(ivaLinea))))));
            conceptos.Add(concepto);
        }
        return conceptos;
    }

    private static void AgregarImpuestosGlobales(XElement comprobante, decimal subtotal, decimal totalImpuestos)
    {
        if (totalImpuestos <= 0) return;
        comprobante.Add(new XElement(NsCfdi + "Impuestos",
            new XAttribute("TotalImpuestosTrasladados", Formato(totalImpuestos)),
            new XElement(NsCfdi + "Traslados",
                new XElement(NsCfdi + "Traslado",
                    new XAttribute("Base", Formato(subtotal)),
                    new XAttribute("Impuesto", "002"),  // c_Impuesto: 002 = IVA
                    new XAttribute("TipoFactor", "Tasa"),
                    new XAttribute("TasaOCuota", "0.160000"),
                    new XAttribute("Importe", Formato(totalImpuestos))))));
    }

    private static string? NormalizarCp(string? cp)
    {
        var limpio = new string((cp ?? string.Empty).Where(char.IsDigit).ToArray());
        return limpio.Length == 5 ? limpio : null;
    }

    private static string ClaveUnidadSat(string unidad) => unidad.Trim().ToUpperInvariant() switch
    {
        "LITRO" => "LTR",  // c_ClaveUnidad: LTR = Litro
        _ => "H87"         // c_ClaveUnidad: H87 = Unidad (pieza): garrafa/tambor/caja
    };

    private static TimeZoneInfo ZonaMexico()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"); }
        catch (Exception) { /* ID IANA no disponible en este sistema */ }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)"); }
        catch (Exception) { return TimeZoneInfo.Local; }
    }

    /// <summary>Número de serie del CSD en decimal de 20 posiciones (XSD: [0-9]{20}).</summary>
    private static string NumeroCertificado(X509Certificate2 cert)
    {
        // cert.SerialNumber es el hex en orden canónico (idéntico a certutil/openssl);
        // GetSerialNumber() devuelve los bytes invertidos, por eso se parsea la propiedad.
        var serial = BigInteger.Parse("0" + cert.SerialNumber, NumberStyles.HexNumber)
            .ToString(CultureInfo.InvariantCulture);
        if (serial.Length > 20) serial = serial[^20..];
        return serial.PadLeft(20, '0');
    }

    private static string CalcularCadenaOriginal(XDocument doc)
    {
        var transform = CadenaXslt.Value;
        using var sw = new StringWriter(CultureInfo.InvariantCulture);
        using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { OmitXmlDeclaration = true }))
        using (var xr = XmlReader.Create(new StringReader(doc.ToString())))
            transform.Transform(xr, xw);
        return sw.ToString();
    }

    private static XslCompiledTransform CargarXslt()
    {
        var asm = typeof(CfdiSelladoService).Assembly;
        var nombre = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("cadenaoriginal_4_0.xslt", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No se encontró el recurso embebido cadenaoriginal_4_0.xslt.");
        using var stream = asm.GetManifestResourceStream(nombre)
            ?? throw new InvalidOperationException($"No se pudo leer el recurso '{nombre}'.");
        var xslt = new XslCompiledTransform();
        // Los xsl:include del SAT (utilerias + complementos) se resuelven contra recursos embebidos.
        using var reader = XmlReader.Create(stream);
        xslt.Load(reader, XsltSettings.Default, new XsltRecursosResolver());
        return xslt;
    }

    /// <summary>
    /// Resuelve los xsl:include del XSLT oficial del SAT hacia los .xslt embebidos en este
    /// ensamblado (match por nombre de archivo; es único en el árbol oficial del SAT).
    /// </summary>
    private sealed class XsltRecursosResolver : XmlResolver
    {
        public override Uri? ResolveUri(Uri? baseUri, string? relativeUri)
            => new Uri("oilsmexico://xslt/"
                + Uri.EscapeDataString(Path.GetFileName(relativeUri ?? string.Empty)));

        public override object? GetEntity(Uri absoluteUri, string? objectType, Type? ofReturnType)
        {
            var archivo = Uri.UnescapeDataString(Path.GetFileName(absoluteUri.LocalPath));
            var asm = typeof(CfdiSelladoService).Assembly;
            var rn = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(archivo, StringComparison.OrdinalIgnoreCase));
            return rn is null ? null : asm.GetManifestResourceStream(rn);
        }
    }

    /// <summary>
    /// Carga la llave privada del CSD. Soporta PKCS#8 encriptado ("ENCRYPTED PRIVATE KEY")
    /// y la forma tradicional OpenSSL con DEK-Info (DES-EDE3-CBC / AES), típica de los CSD del SAT.
    /// </summary>
    internal static RSA CargarLlavePrivada(string keyPath, string password)
    {
        var pem = File.ReadAllText(keyPath);
        var rsa = RSA.Create();
        try
        {
            if (pem.Contains("ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
                rsa.ImportEncryptedPkcs8PrivateKey(Encoding.UTF8.GetBytes(password), ExtraerDer(pem), out _);
            else if (pem.Contains("DEK-Info", StringComparison.OrdinalIgnoreCase))
                rsa.ImportRSAPrivateKey(DescifrarPemTradicional(pem, password), out _);
            else
                rsa.ImportRSAPrivateKey(ExtraerDer(pem), out _);
            return rsa;
        }
        catch (Exception ex)
        {
            rsa.Dispose();
            throw new InvalidOperationException(
                $"No se pudo leer la llave privada '{keyPath}' (¿contraseña de CSD incorrecta?): {ex.Message}", ex);
        }
    }

    private static byte[] ExtraerDer(string pem)
    {
        var cuerpo = string.Join("\n", pem.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0
                && !l.StartsWith("-----", StringComparison.Ordinal)
                && !l.Contains(':'))); // descarta encabezados ("Proc-Type:", "DEK-Info:")
        return Convert.FromBase64String(cuerpo);
    }

    private static byte[] DescifrarPemTradicional(string pem, string password)
    {
        var lineaDek = pem.Split('\n')
                .FirstOrDefault(l => l.TrimStart().StartsWith("DEK-Info", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("El PEM de la llave no incluye DEK-Info (formato no reconocido).");
        var partes = lineaDek.Split(':', 2)[1].Split(',');
        var cipher = partes[0].Trim().ToUpperInvariant();
        var iv = Convert.FromHexString(partes[1].Trim());
        var keyLen = cipher switch
        {
            "DES-EDE3-CBC" => 24,
            "AES-128-CBC" => 16,
            "AES-192-CBC" => 24,
            "AES-256-CBC" => 32,
            _ => throw new InvalidOperationException(
                $"Algoritmo de cifrado de llave no soportado: {cipher} (usa DES-EDE3-CBC o AES-n-CBC).")
        };
        // EVP_BytesToKey (MD5, 1 iteración) con sal = primeros 8 bytes del IV, como OpenSSL.
        var clave = DerivarClaveOpenssl(password, iv.Take(8).ToArray(), keyLen);
        using var alg = cipher switch
        {
            "DES-EDE3-CBC" => (SymmetricAlgorithm)TripleDES.Create(),
            _ => Aes.Create()
        };
        alg.Mode = CipherMode.CBC;
        alg.Padding = PaddingMode.PKCS7;
        alg.Key = clave;
        alg.IV = iv;
        var der = ExtraerDer(pem);
        using var decryptor = alg.CreateDecryptor();
        return decryptor.TransformFinalBlock(der, 0, der.Length);
    }

    private static byte[] DerivarClaveOpenssl(string password, byte[] sal, int longitud)
    {
        var pass = Encoding.UTF8.GetBytes(password);
        using var md5 = MD5.Create();
        var acumulado = new List<byte>(longitud + 16);
        byte[] previo = [];
        while (acumulado.Count < longitud)
        {
            var entrada = new byte[previo.Length + pass.Length + sal.Length];
            previo.CopyTo(entrada, 0);
            pass.CopyTo(entrada, previo.Length);
            sal.CopyTo(entrada, previo.Length + pass.Length);
            previo = md5.ComputeHash(entrada);
            acumulado.AddRange(previo);
        }
        return acumulado.Take(longitud).ToArray();
    }
}

/// <summary>
/// Cliente del PAC Finkok (SOAP document/literal):
/// Cfdi:PacModo=SIMULADO → UUID local auditable (sin red);
/// TEST → https://demo-facturacion.finkok.com; PRODUCCION → https://facturacion.finkok.com.
/// El XML viaja en base64 (WSDL: xs:base64Binary) y la respuesta trae el CFDI timbrado.
/// </summary>
public sealed class PacTimbradoService(
    IConfiguration cfg, HttpClient http, ErpDbContext db) : IPacTimbradoService
{
    private static readonly XNamespace NsDs = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly XNamespace NsCancela = "http://cancelacion.sat.gob.mx";

    public async Task<(Guid Uuid, string XmlTimbrado)> TimbrarAsync(
        string xmlSellado, CancellationToken ct = default)
    {
        var modo = cfg["Cfdi:PacModo"] ?? "SIMULADO";
        if (string.Equals(modo, "SIMULADO", StringComparison.OrdinalIgnoreCase))
            return (Guid.NewGuid(), xmlSellado);

        if (!string.Equals(modo, "TEST", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(modo, "PRODUCCION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cfdi:PacModo inválido: '{modo}'. Valores permitidos: SIMULADO, TEST, PRODUCCION.");

        var (usuario, password, urlBase) = DatosPac(modo);
        var url = urlBase + "/servicios/soap/stamp";
        var envelope = ArmarEnvelope(xmlSellado, usuario, password);

        for (var intento = 1; ; intento++)
        {
            var (status, cuerpo) = await PostSoapAsync(url, envelope, "stamp", ct);
            var doc = ParsearRespuesta(cuerpo, status);

            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault is not null)
            {
                var fs = Hijo(fault, "faultstring");
                if (fs.Length == 0)
                    fs = string.Join(" ", fault.Descendants().Select(d => d.Value)
                        .Where(v => !string.IsNullOrWhiteSpace(v)));
                throw new InvalidOperationException(
                    $"Finkok devolvió un error SOAP (modo {modo}): {Hijo(fault, "faultcode")} {fs}".Trim());
            }

            var res = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "stampResult")
                ?? throw new InvalidOperationException(
                    $"Finkok respondió sin stampResult (HTTP {status}): {Recortar(cuerpo)}");

            var uuidTxt = Hijo(res, "UUID");
            var xmlTxt = Hijo(res, "xml");
            var codEstatus = Hijo(res, "CodEstatus");
            var faultStr = Hijo(res, "faultstring");
            var faultCode = Hijo(res, "faultcode");
            var incidencias = res.Descendants()
                .Where(e => e.Name.LocalName == "Incidencia")
                .Select(i => (Codigo: Hijo(i, "CodigoError"), Mensaje: Hijo(i, "MensajeIncidencia")))
                .ToList();

            if (Guid.TryParse(uuidTxt, out var uuid) && xmlTxt.Trim().Length > 0)
                return (uuid, NormalizarXml(xmlTxt));

            // Código 307 de Finkok: timbrado en buffer; reintento corto (igual que la librería phpcfdi).
            if (incidencias.Any(i => i.Codigo == "307") && intento < 3)
            {
                await Task.Delay(250, ct);
                continue;
            }

            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(faultStr))
                partes.Add(faultStr + (faultCode.Length > 0 ? $" ({faultCode})" : ""));
            if (!string.IsNullOrWhiteSpace(codEstatus))
                partes.Add($"CodEstatus {codEstatus}");
            if (Guid.TryParse(uuidTxt, out _))
                partes.Add($"UUID {uuidTxt} sin XML de timbrado");
            partes.AddRange(incidencias
                .Where(i => i.Codigo.Length > 0 || i.Mensaje.Length > 0)
                .Select(i => $"{i.Codigo} {i.Mensaje}".Trim()));
            if (partes.Count == 0)
                partes.Add($"HTTP {status}: {Recortar(cuerpo)}");
            throw new InvalidOperationException(
                $"Finkok no timbró el CFDI (modo {modo}): {string.Join(" | ", partes)}");
        }
    }

    public async Task CancelarAsync(
        Guid uuid, string motivoSat, string? folioSustitucion = null, CancellationToken ct = default)
    {
        var modo = cfg["Cfdi:PacModo"] ?? "SIMULADO";
        if (string.Equals(modo, "SIMULADO", StringComparison.OrdinalIgnoreCase)) return;
        var motivo = (motivoSat ?? string.Empty).Trim();
        if (motivo is not ("01" or "02" or "03" or "04"))
            throw new InvalidOperationException(
                $"Motivo de cancelación SAT inválido: '{motivo}'. Usa 01, 02, 03 o 04.");
        if (motivo == "01" && !Guid.TryParse(folioSustitucion, out _))
            throw new InvalidOperationException("El motivo 01 exige el UUID del CFDI sustituto.");
        var (usuario, password, urlBase) = DatosPac(modo);
        var cancelXml = FirmarSolicitudCancelacion(uuid, motivo,
            motivo == "01" ? folioSustitucion!.Trim() : null);
        var url = urlBase + "/servicios/soap/cancel";
        var envelope = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
            "xmlns:ca=\"http://facturacion.finkok.com/cancel\">" +
            "<soapenv:Header/><soapenv:Body><ca:cancel_signature>" +
            $"<ca:xml>{Convert.ToBase64String(Encoding.UTF8.GetBytes(cancelXml))}</ca:xml>" +
            $"<ca:username>{Escapar(usuario)}</ca:username>" +
            $"<ca:password>{Escapar(password)}</ca:password>" +
            "<ca:store_pending>true</ca:store_pending>" +
            "</ca:cancel_signature></soapenv:Body></soapenv:Envelope>";
        var (status, cuerpo) = await PostSoapAsync(url, envelope, "cancel_signature", ct);
        var doc = ParsearRespuesta(cuerpo, status);
        var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
            throw new InvalidOperationException(
                $"Finkok devolvió un error SOAP al cancelar (modo {modo}): {Recortar(fault.Value.Trim())}");
        var res = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "cancel_signatureResult")
            ?? throw new InvalidOperationException(
                $"Finkok respondió sin cancel_signatureResult (HTTP {status}): {Recortar(cuerpo)}");
        var codEstatus = Hijo(res, "CodEstatus");
        var folio = res.Descendants().FirstOrDefault(e => e.Name.LocalName == "Folio");
        var estatusUuid = folio is null ? string.Empty : Hijo(folio, "EstatusUUID");
        var estatusCanc = folio is null ? string.Empty : Hijo(folio, "EstatusCancelacion");
        if (!codEstatus.StartsWith("201", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Finkok no canceló el UUID {uuid} (modo {modo}): CodEstatus {codEstatus} " +
                $"EstatusUUID {estatusUuid} EstatusCancelacion {estatusCanc}".Trim());
    }

    public async Task<string> EstadoSatAsync(Guid uuid, CancellationToken ct = default)
    {
        var modo = cfg["Cfdi:PacModo"] ?? "SIMULADO";
        if (string.Equals(modo, "SIMULADO", StringComparison.OrdinalIgnoreCase)) return "SIMULADO";
        var (usuario, password, urlBase) = DatosPac(modo);
        var fila = await db.Facturas.AsNoTracking()
            .Where(f => f.UuidSat == uuid)
            .Select(f => new { f.Total, Rfc = f.Sucursal != null ? f.Sucursal.RfcEmisor : null })
            .FirstOrDefaultAsync(ct);
        var rfc = fila?.Rfc?.Trim() ?? await db.Sucursales.AsNoTracking()
            .Select(s => s.RfcEmisor).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No hay RFC emisor para consultar el SAT.");
        var url = urlBase + "/servicios/soap/cancel";
        var envelope = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
            "xmlns:ca=\"http://facturacion.finkok.com/cancel\">" +
            "<soapenv:Header/><soapenv:Body><ca:get_sat_status>" +
            $"<ca:username>{Escapar(usuario)}</ca:username>" +
            $"<ca:password>{Escapar(password)}</ca:password>" +
            $"<ca:taxpayer_id>{Escapar(rfc)}</ca:taxpayer_id>" +
            "<ca:rtaxpayer_id></ca:rtaxpayer_id>" +
            $"<ca:uuid>{uuid.ToString().ToUpperInvariant()}</ca:uuid>" +
            $"<ca:total>{(fila?.Total ?? 0m).ToString("0.00", CultureInfo.InvariantCulture)}</ca:total>" +
            "</ca:get_sat_status></soapenv:Body></soapenv:Envelope>";
        var (status, cuerpo) = await PostSoapAsync(url, envelope, "get_sat_status", ct);
        var doc = ParsearRespuesta(cuerpo, status);
        var res = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "get_sat_statusResult");
        if (res is null) return $"HTTP {status}";
        var sat = res.Descendants().FirstOrDefault(e => e.Name.LocalName == "sat");
        if (sat is null) return Hijo(res, "error");
        return $"Estado {Hijo(sat, "Estado")} | Cancelable {Hijo(sat, "EsCancelable")} | " +
            $"EstatusCancelacion {Hijo(sat, "EstatusCancelacion")} | Codigo {Hijo(sat, "CodigoEstatus")}";
    }

    private static void ValidarModo(string modo)
    {
        if (!string.Equals(modo, "TEST", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(modo, "PRODUCCION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cfdi:PacModo inválido: '{modo}'. Valores permitidos: SIMULADO, TEST, PRODUCCION.");
    }

    private (string Usuario, string Password, string UrlBase) DatosPac(string modo)
    {
        ValidarModo(modo);
        var usuario = cfg["Cfdi:Pac:Usuario"];
        var password = cfg["Cfdi:Pac:Password"];
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Faltan credenciales de Finkok: configura Cfdi:Pac:Usuario y Cfdi:Pac:Password " +
                "(user-secrets o variables de entorno Cfdi__Pac__Usuario / Cfdi__Pac__Password).");
        var urlBase = cfg["Cfdi:Pac:UrlBase"];
        if (string.IsNullOrWhiteSpace(urlBase))
            urlBase = string.Equals(modo, "TEST", StringComparison.OrdinalIgnoreCase)
                ? "https://demo-facturacion.finkok.com"
                : "https://facturacion.finkok.com";
        return (usuario, password, urlBase.TrimEnd('/'));
    }

    private string FirmarSolicitudCancelacion(Guid uuid, string motivo, string? folioSustitucion)
    {
        var cerPath = cfg["Cfdi:CerPath"] ?? "certs/csd.cer";
        var keyPath = cfg["Cfdi:KeyPath"] ?? "certs/csd.key";
        var password = cfg["Cfdi:KeyPassword"] ?? string.Empty;
        if (!File.Exists(cerPath) || !File.Exists(keyPath))
            throw new InvalidOperationException(
                $"La cancelación real requiere el CSD: no se encontró '{cerPath}' o '{keyPath}'.");
        using var cert = X509CertificateLoader.LoadCertificateFromFile(cerPath);
        using var rsa = CfdiSelladoService.CargarLlavePrivada(keyPath, password);
        var rfc = db.Sucursales.AsNoTracking().Select(s => s.RfcEmisor).FirstOrDefault()
            ?? throw new InvalidOperationException("No hay sucursal con RFC emisor para firmar la cancelación.");
        var cp = db.Sucursales.AsNoTracking().Select(s => s.CodigoPostal).FirstOrDefault() ?? "06600";
        var fecha = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var folio = new XElement(NsCancela + "Folio",
            new XAttribute("UUID", uuid.ToString().ToUpperInvariant()),
            new XAttribute("Motivo", motivo));
        folio.Add(motivo == "01"
            ? new XAttribute("FolioSustitucion", folioSustitucion!.ToUpperInvariant())
            : new XAttribute("FolioSustitucion", string.Empty));
        var cancelacion = new XElement(NsCancela + "Cancelacion",
            new XAttribute("Fecha", fecha),
            new XAttribute("RfcEmisor", rfc.Trim()),
            new XAttribute("LugarExpedicion", (cp ?? "06600").Trim()),
            new XElement(NsCancela + "Folios", folio));
        var xmlSinFirma = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine
            + cancelacion.ToString(SaveOptions.DisableFormatting);
        byte[] digest;
        using (var sha = SHA256.Create())
            digest = sha.ComputeHash(Encoding.UTF8.GetBytes(xmlSinFirma));
        var signedInfo = new XElement(NsDs + "SignedInfo",
            new XElement(NsDs + "CanonicalizationMethod",
                new XAttribute("Algorithm", "http://www.w3.org/TR/2001/REC-xml-c14n-20010315")),
            new XElement(NsDs + "SignatureMethod",
                new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256")),
            new XElement(NsDs + "Reference", new XAttribute("URI", string.Empty),
                new XElement(NsDs + "DigestMethod",
                    new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")),
                new XElement(NsDs + "DigestValue", Convert.ToBase64String(digest))));
        var firma = Convert.ToBase64String(rsa.SignData(
            Encoding.UTF8.GetBytes(signedInfo.ToString(SaveOptions.DisableFormatting)),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        cancelacion.Add(new XElement(NsDs + "Signature",
            signedInfo,
            new XElement(NsDs + "SignatureValue", firma),
            new XElement(NsDs + "KeyInfo",
                new XElement(NsDs + "X509Data",
                    new XElement(NsDs + "X509Certificate",
                        Convert.ToBase64String(cert.GetRawCertData()))))));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine
            + cancelacion.ToString(SaveOptions.DisableFormatting);
    }

    private static string ArmarEnvelope(string xml, string usuario, string password)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" " +
            "xmlns:st=\"http://facturacion.finkok.com/stamp\">" +
            "<soapenv:Header/><soapenv:Body><st:stamp>" +
            $"<st:xml>{b64}</st:xml>" +
            $"<st:username>{Escapar(usuario)}</st:username>" +
            $"<st:password>{Escapar(password)}</st:password>" +
            "</st:stamp></soapenv:Body></soapenv:Envelope>";
    }

    private async Task<(int Status, string Cuerpo)> PostSoapAsync(
        string url, string envelope, string accion, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        req.Headers.TryAddWithoutValidation("SOAPAction", accion);
        try
        {
            using var resp = await http.SendAsync(req, ct);
            return ((int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"No se pudo contactar a Finkok ({url}): {ex.Message}", ex);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Tiempo de espera agotado al contactar a Finkok ({url}).");
        }
    }

    private static XDocument ParsearRespuesta(string cuerpo, int status)
    {
        try { return XDocument.Parse(cuerpo); }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Respuesta inválida de Finkok (HTTP {status}): {Recortar(cuerpo)}", ex);
        }
    }

    private static string Hijo(XElement e, string local)
        => e.Elements().FirstOrDefault(x => x.Name.LocalName == local)?.Value.Trim() ?? string.Empty;

    private static string NormalizarXml(string xml)
    {
        var t = xml.Trim();
        if (!t.StartsWith("<", StringComparison.Ordinal))
        {
            try { t = Encoding.UTF8.GetString(Convert.FromBase64String(t)).Trim(); }
            catch (FormatException) { /* no era base64: se usa tal cual */ }
        }
        if (!t.StartsWith("<?xml", StringComparison.Ordinal))
            t = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + t;
        return t;
    }

    private static string Escapar(string v) => v
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("'", "&apos;");

    private static string Recortar(string texto)
        => texto.Length <= 400 ? texto : texto[..400] + "…";
}

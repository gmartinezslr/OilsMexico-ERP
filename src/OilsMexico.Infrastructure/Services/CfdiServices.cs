using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// REGLA #2 — Sellado nativo CFDI 4.0:
/// lee CSD (.cer + .key DER de OpenSSL), arma cadena original y firma RSA-SHA256
/// con System.Security.Cryptography. Sin librerías de terceros.
/// </summary>
public sealed class CfdiSelladoService(ErpDbContext db, IConfiguration cfg) : ICfdiSelladoService
{
    public async Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarAsync(
        int facturaId, CancellationToken ct = default)
    {
        var f = await db.Facturas
            .Include(x => x.Detalles).ThenInclude(d => d.Producto)
            .Include(x => x.Cliente).Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == facturaId, ct)
            ?? throw new InvalidOperationException("Factura no existe.");

        var cerPath = cfg["Cfdi:CerPath"] ?? "certs/csd.cer";
        var keyPath = cfg["Cfdi:KeyPath"] ?? "certs/csd.key";
        var password = cfg["Cfdi:KeyPassword"] ?? string.Empty;

        if (!File.Exists(cerPath) || !File.Exists(keyPath))
        {
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

        var cert = X509CertificateLoader.LoadCertificateFromFile(cerPath);
        var keyDer = await File.ReadAllBytesAsync(keyPath, ct);
        using var rsa = RSA.Create();
        rsa.ImportEncryptedPkcs8PrivateKey(Encoding.UTF8.GetBytes(password), keyDer, out _);

        XNamespace cfdi = "http://www.sat.gob.mx/cfd/4";
        var comprobante = new XElement(cfdi + "Comprobante",
            new XAttribute("Version", "4.0"),
            new XAttribute("Folio", f.FolioInterno),
            new XAttribute("Fecha", f.FechaEmision.ToString("yyyy-MM-ddTHH:mm:ss")),
            new XAttribute("FormaPago", f.FormaPagoSat),
            new XAttribute("MetodoPago", f.MetodoPagoSat),
            new XAttribute("SubTotal", f.Subtotal.ToString("N2")),
            new XAttribute("Total", f.Total.ToString("N2")));

        var cadena = $"||4.0|{f.FolioInterno}|{f.FechaEmision:yyyy-MM-ddTHH:mm:ss}|" +
            $"{f.FormaPagoSat}|{f.MetodoPagoSat}|{f.Subtotal:N2}|{f.Total:N2}|" +
            $"{f.Sucursal?.RfcEmisor}|{f.Cliente?.Rfc}|{cert.Thumbprint}|";
        var firma = rsa.SignData(Encoding.UTF8.GetBytes(cadena),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sello = Convert.ToBase64String(firma);
        comprobante.SetAttributeValue("Sello", sello);
        comprobante.SetAttributeValue("Certificado",
            Convert.ToBase64String(cert.GetRawCertData()));

        var xml = new XDocument(new XDeclaration("1.0", "UTF-8", null), comprobante).ToString();
        f.CadenaOriginal = cadena; f.SelloDigital = sello; f.XmlSellado = xml;
        await db.SaveChangesAsync(ct);
        return (cadena, sello, xml);
    }
}

/// <summary>Cliente PAC: en producción aquí va el SOAP/REST real; en dev genera UUID.</summary>
public sealed class PacTimbradoService(IConfiguration cfg, HttpClient? http = null) : IPacTimbradoService
{
    public Task<Guid> TimbrarAsync(string xmlSellado, CancellationToken ct = default)
    {
        var modo = cfg["Cfdi:PacModo"] ?? "SIMULADO";
        if (modo == "SIMULADO" || http is null) return Task.FromResult(Guid.NewGuid());
        // TODO: picks PAC (Finkok/SW/Stamping.io): POST SOAP con xmlSellado.
        throw new NotImplementedException("Configura Cfdi:PacUrl y credenciales del PAC.");
    }
}

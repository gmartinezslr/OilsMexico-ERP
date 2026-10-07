$ErrorActionPreference = 'Stop'
$out = New-Object System.Collections.Generic.List[string]
$base = "$env:TEMP\cfdv40test"

# ---------- 1. Certificado de prueba (almacen Windows) ----------
$cert2 = Get-ChildItem Cert:\LocalMachine\CA | Where-Object { $_.HasPrivateKey -eq $false } | Select-Object -First 1
if (-not $cert2) { $cert2 = Get-ChildItem Cert:\CurrentUser\Root | Select-Object -First 1 }
$hexProp = $cert2.SerialNumber
$out.Add("=== SERIE (CN=$($cert2.Subject.Substring(0,[Math]::Min(50,$cert2.Subject.Length)))) ===")
$out.Add("SerialNumber prop .NET : $hexProp")
[IO.File]::WriteAllBytes("$env:TEMP\testcert.cer", $cert2.RawData)
& certutil -dump "$env:TEMP\testcert.cer" 2>&1 | Select-String -Pattern 'serie' | Select-Object -First 2 | ForEach-Object { $out.Add("certutil: $_") }
$bi = [Numerics.BigInteger]::Parse("0$hexProp", 'HexNumber')
$dec = $bi.ToString()
$out.Add("decimal canonico: $dec (len $($dec.Length))")

# ---------- 2. Muestra CFDI 4.0 ----------
$dec20 = $dec.PadLeft(20, '0'); if ($dec20.Length -gt 20) { $dec20 = $dec20.Substring($dec20.Length - 20) }
$b64cert = [Convert]::ToBase64String($cert2.RawData)
$sample = @"
<?xml version="1.0" encoding="UTF-8"?>
<cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Version="4.0" Folio="V-1-20260610153000" Fecha="2026-06-10T15:30:00" FormaPago="03" MetodoPago="PUE" SubTotal="100.00" Moneda="MXN" Total="116.00" TipoDeComprobante="I" Exportacion="01" LugarExpedicion="03810" NoCertificado="$dec20" Certificado="$b64cert" Sello="QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejAxMjM0NTY3ODk=">
  <cfdi:Emisor Rfc="OLU090101AAA" Nombre="OilsMexico Aceites y Lubricantes S.A. de C.V." RegimenFiscal="601"/>
  <cfdi:Receptor Rfc="XAXX010101000" Nombre="Publico en general" DomicilioFiscalReceptor="06600" RegimenFiscalReceptor="616" UsoCFDI="G03"/>
  <cfdi:Conceptos>
    <cfdi:Concepto ClaveProdServ="01010101" Cantidad="2" ClaveUnidad="LTR" Unidad="Litro" Descripcion="Aceite 5W-30" NoIdentificacion="SKU-001" ValorUnitario="50.00" Importe="100.00" ObjetoImp="02">
      <cfdi:Impuestos><cfdi:Traslados><cfdi:Traslado Base="100.00" Impuesto="02" TipoFactor="Tasa" TasaOCuota="0.160000" Importe="16.00"/></cfdi:Traslados></cfdi:Impuestos>
    </cfdi:Concepto>
  </cfdi:Conceptos>
  <cfdi:Impuestos TotalImpuestosTraslados="16.00"/>
</cfdi:Comprobante>
"@
[IO.File]::WriteAllText("$env:TEMP\sample_cfdi.xml", $sample, [Text.UTF8Encoding]::new($false))

# ---------- 3. Validación XSD (3 schemas agregados manualmente) ----------
$out.Add("=== VALIDACION XSD ===")
try {
    $set = New-Object System.Xml.Schema.XmlSchemaSet
    $r1 = [Xml.XmlReader]::Create("$base\4\cfdv40.xsd")
    $r2 = [Xml.XmlReader]::Create("$base\catalogos\catCFDI.xsd")
    $r3 = [Xml.XmlReader]::Create("$base\tipoDatos\tdCFDI\tdCFDI.xsd")
    $set.Add('http://www.sat.gob.mx/cfd/4', $r1) | Out-Null
    $set.Add('http://www.sat.gob.mx/sitio_internet/cfd/catalogos', $r2) | Out-Null
    $set.Add('http://www.sat.gob.mx/sitio_internet/cfd/tipoDatos/tdCFDI', $r3) | Out-Null
    $set.Compile()
    $r1.Close(); $r2.Close(); $r3.Close()
    $doc = New-Object Xml.XmlDocument
    $doc.Load("$env:TEMP\sample_cfdi.xml")
    $errs = New-Object System.Collections.Generic.List[string]
    $doc.Schemas = $set
    $doc.Validate({ param($s, $e) $errs.Add($e.Message) })
    if ($errs.Count -eq 0) { $out.Add("XML de muestra: VALIDO contra cfdv40.xsd OK") }
    else { $errs | ForEach-Object { $out.Add("XSD ERROR: $_") } }
} catch { $out.Add("XSD EXCEPCION: $($_.Exception.Message)") }

# ---------- 4. Cadena original (XSLT desde el workspace, con includes) ----------
$out.Add("=== CADENA ORIGINAL (XSLT) ===")
try {
    $xsltPath = 'D:\GMS\Jerry\OilsMexico\src\OilsMexico.Infrastructure\Cfdi\xslt\4\cadenaoriginal_4_0\cadenaoriginal_4_0.xslt'
    $xslt = New-Object System.Xml.Xsl.XslCompiledTransform
    $xslt.Load($xsltPath, [System.Xml.Xslt.XsltSettings]::Default, (New-Object System.Xml.XmlUrlResolver))
    $sw = New-Object System.IO.StringWriter
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.OmitXmlDeclaration = $true
    $xw = [Xml.XmlWriter]::Create($sw, $settings)
    $xslt.Transform([Xml.XmlReader]::Create("$env:TEMP\sample_cfdi.xml"), $xw)
    $xw.Flush(); $xw.Close()
    $cadena = $sw.ToString()
    $out.Add("lenght=$($cadena.Length)")
    $out.Add("inicio: " + $cadena.Substring(0, [Math]::Min(260, $cadena.Length)))
    $out.Add("fin: ..." + $cadena.Substring([Math]::Max(0, $cadena.Length - 80)))
} catch {
    $ex = $_.Exception
    while ($ex) { $out.Add("XSLT EX: [$($ex.GetType().Name)] $($ex.Message)"); $ex = $ex.InnerException }
}

# ---------- 5. Patrones de montos (tdCFDI) ----------
$out.Add("=== PATRONES tdCFDI ===")
$td = Get-Content "$base\tipoDatos\tdCFDI\tdCFDI.xsd" -Raw
$out.Add("tdCFDI inicio: " + $td.Substring(0, 80))
foreach ($n in 't_Cantidad', 't_ValorUnitario', 't_Importe', 't_FechaH') {
    $i = $td.IndexOf("simpleType name=`"$n`"")
    if ($i -ge 0) {
        $seg = $td.Substring($i, 800)
        $p = [regex]::Matches($seg, 'pattern value="([^"]+)"') | Select-Object -First 2
        $out.Add("${n}: " + (($p | ForEach-Object { $_.Groups[1].Value }) -join ' ; '))
    } else { $out.Add("${n}: no encontrado") }
}

[IO.File]::WriteAllLines("$env:TEMP\validacion1.txt", $out)
Write-Output "LISTO -> $env:TEMP\validacion1.txt"
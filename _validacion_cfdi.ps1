$ErrorActionPreference = 'Stop'
$out = New-Object System.Collections.Generic.List[string]

# ---------- 1. Imports del XSD ----------
$xsdRaw = [IO.File]::ReadAllText("$env:TEMP\cfdv40.xsd")
$out.Add("=== IMPORTS de cfdv40.xsd ===")
[regex]::Matches($xsdRaw, '<xs:import[^>]*>') | ForEach-Object { $out.Add($_.Value) }

# Localizar schemaLocation del catálogo para resolución local
$xsdLocal = $xsdRaw
foreach ($m in [regex]::Matches($xsdRaw, 'schemaLocation="([^"]+)"')) {
    $loc = $m.Groups[1].Value
    if ($loc -match 'catCFDI\.xsd$' -or $loc -match '/catCFDI') {
        $xsdLocal = $xsdLocal -replace [regex]::Escape($m.Value), 'schemaLocation="catCFDI.xsd"'
    }
}
[IO.File]::WriteAllText("$env:TEMP\cfdv40_local.xsd", $xsdLocal)

# ---------- 2. Certificado de prueba (almacen Windows) ----------
$cert2 = Get-ChildItem Cert:\LocalMachine\CA | Where-Object { $_.HasPrivateKey -eq $false } | Select-Object -First 1
if (-not $cert2) { $cert2 = Get-ChildItem Cert:\CurrentUser\Root | Select-Object -First 1 }
$bytes = $cert2.GetSerialNumber()
$hexBE = ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
$hexLE = (($bytes[($bytes.Length - 1)..0] | ForEach-Object { $_.ToString('x2') }) -join '')
$out.Add("=== SERIE DEL CERTIFICADO ( $($cert2.Subject.Substring(0,[Math]::Min(60,$cert2.Subject.Length))) ) ===")
$out.Add("GetSerialNumber(hex tal cual) : $hexBE")
$out.Add("SerialNumber (prop .NET)      : $($cert2.SerialNumber)")
[IO.File]::WriteAllBytes("$env:TEMP\testcert.cer", $cert2.RawData)
$cu = & certutil -dump "$env:TEMP\testcert.cer" 2>&1 | Select-String -Pattern 'Serial|serie' | Select-Object -First 3
$cu | ForEach-Object { $out.Add("certutil: $_") }
$biBE = [Numerics.BigInteger]::Parse("0$hexBE", 'HexNumber')
$biLE = [Numerics.BigInteger]::Parse("0$hexLE", 'HexNumber')
$out.Add("decimal asumiendo big-endian    : $($biBE.ToString())  (len $($biBE.ToString().Length))")
$out.Add("decimal asumiendo little-endian : $($biLE.ToString())  (len $($biLE.ToString().Length))")

# ---------- 3. Muestra CFDI 4.0 (idéntico a la salida del generador) ----------
$decBE = $biBE.ToString().PadLeft(20, '0')
if ($decBE.Length -gt 20) { $decBE = $decBE.Substring($decBE.Length - 20) }
$b64cert = [Convert]::ToBase64String($cert2.RawData)
$sample = @"
<?xml version="1.0" encoding="UTF-8"?>
<cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" Version="4.0" Folio="V-1-20260610153000" Fecha="2026-06-10T15:30:00" FormaPago="03" MetodoPago="PUE" SubTotal="100.00" Moneda="MXN" Total="116.00" TipoDeComprobante="I" Exportacion="01" LugarExpedicion="03810" NoCertificado="$decBE" Certificado="$b64cert" Sello="QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVphYmNkZWZnaGlqa2xtbm9wcXJzdHV2d3h5ejAxMjM0NTY3ODk=">
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

# ---------- 4. Validación XSD ----------
$out.Add("=== VALIDACION XSD (cfdv40 + catCFDI) ===")
try {
    $set = New-Object System.Xml.Schema.XmlSchemaSet
    $reader = [Xml.XmlReader]::Create("$env:TEMP\cfdv40_local.xsd")
    $set.Add('http://www.sat.gob.mx/cfd/4', $reader) | Out-Null
    $set.Compile()
    $doc = New-Object Xml.XmlDocument
    $doc.Load("$env:TEMP\sample_cfdi.xml")
    $errs = New-Object System.Collections.Generic.List[string]
    $doc.Schemas = $set
    $doc.Validate({ param($s, $e) $errs.Add($e.Message) })
    if ($errs.Count -eq 0) { $out.Add("XML de muestra: VALIDO contra cfdv40.xsd ✓") }
    else { $errs | ForEach-Object { $out.Add("XSD ERROR: $_") } }
} catch { $out.Add("XSD EXCEPCION: $($_.Exception.Message)") }

# ---------- 5. Cadena original (XSLT) ----------
$out.Add("=== CADENA ORIGINAL (XSLT) ===")
try {
    $xslt = New-Object System.Xml.Xsl.XslCompiledTransform
    $xslt.Load("$env:TEMP\cadenaoriginal_4_0.xslt")
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
} catch { $out.Add("XSLT EXCEPCION: $($_.Exception.Message)") }

# ---------- 6. Patrones XSD de montos ----------
$out.Add("=== PATRONES XSD ===")
foreach ($n in 't_Cantidad', 't_ValorUnitario', 't_Importe') {
    $i = $xsdRaw.IndexOf("simpleType name=`"$n`"")
    if ($i -ge 0) {
        $seg = $xsdRaw.Substring($i, 700)
        $p = [regex]::Match($seg, 'pattern value="([^"]+)"')
        $out.Add("${n}: $($p.Groups[1].Value)")
    } else { $out.Add("${n}: no encontrado") }
}

[IO.File]::WriteAllLines("$env:TEMP\validacion1.txt", $out)
Write-Output "LISTO -> $env:TEMP\validacion1.txt"

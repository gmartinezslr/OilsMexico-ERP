$ErrorActionPreference = 'Stop'
$raw = 'https://raw.githubusercontent.com/phpcfdi/resources-sat-xml/master/resources/www.sat.gob.mx/sitio_internet/cfd/'
$dest = 'D:\GMS\Jerry\OilsMexico\src\OilsMexico.Infrastructure\Cfdi\xslt\'

# Rutas relativas de los includes respecto a 4/cadenaoriginal_4_0/
$pendientes = New-Object System.Collections.Generic.Queue[string]
$pendientes.Enqueue('4/cadenaoriginal_4_0/cadenaoriginal_4_0.xslt')
$hechos = New-Object System.Collections.Generic.HashSet[string]
$reporte = New-Object System.Collections.Generic.List[string]

while ($pendientes.Count -gt 0) {
    $ruta = $pendientes.Dequeue()
    if (-not $hechos.Add($ruta)) { continue }

    # Resolver rel al dir del archivo actual
    $dirRuta = if ($ruta -match '^(.*)/[^/]+$') { $Matches[1] } else { '' }
    $archivoLocal = Join-Path $dest ($ruta -replace '/', '\')
    New-Item -ItemType Directory -Force -Path (Split-Path $archivoLocal) | Out-Null
    if (-not (Test-Path $archivoLocal)) {
        Invoke-WebRequest -Uri ($raw + $ruta) -TimeoutSec 30 -UseBasicParsing -OutFile $archivoLocal
        $reporte.Add("OK  $ruta ($((Get-Item $archivoLocal).Length) bytes)")
    }

    $contenido = [IO.File]::ReadAllText($archivoLocal)
    foreach ($m in [regex]::Matches($contenido, 'href="([^"]+\.xslt)"')) {
        $href = $m.Groups[1].Value
        # resolver dirRuta + href con normalizacion de ..
        $segs = @($dirRuta -split '/' | Where-Object { $_ })
        foreach ($p in ($href -split '/')) {
            if ($p -eq '..') {
                if ($segs.Count -eq 1) { $segs = @() }
                elseif ($segs.Count -gt 1) { $segs = $segs[0..($segs.Count - 2)] }
            }
            elseif ($p -ne '.' -and $p -ne '') { $segs += $p }
        }
        $resuelta = $segs -join '/'
        if ($resuelta -notlike '*..*') { $pendientes.Enqueue($resuelta) }
        else { $reporte.Add("NO-RESUELTO: $resuelta (desde $ruta)") }
    }
}

$reporte.Add("---")
$reporte.Add("total archivos: $($hechos.Count)")
[IO.File]::WriteAllLines("$env:TEMP\descarga_xslt.txt", $reporte)
Write-Output "DESCARGA LISTA: $($hechos.Count) archivos"
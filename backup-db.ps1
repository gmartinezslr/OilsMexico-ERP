<#
.SYNOPSIS
  Backup de la base de datos PostgreSQL del ERP OilsMexico (oilsmexico_erp).
.DESCRIPTION
  Genera un respaldo con pg_dump (formato custom, compresion 9).
  Lee valores por defecto de src/OilsMexico.Web/appsettings.json.
.USO
  powershell -ExecutionPolicy Bypass -File .\backup-db.ps1
  $env:PGPASSWORD='TU_PASSWORD'; .\backup-db.ps1
  .\backup-db.ps1 -DbName oilsmexico_erp -DbUser postgres -BackupDir .\backups -RetainDays 30
#>
[CmdletBinding()]
param(
    [string]$DbName = '',
    [string]$DbUser = '',
    [string]$DbHost = '',
    [int]$DbPort = 0,
    [string]$BackupDir = '',
    [int]$RetainDays = 30,
    [string]$PgBinPath = '',
    [string]$DbPassword = '',
    [string]$AppsettingsPath = ''
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($raiz)) { $raiz = (Get-Location).Path }
Set-Location $raiz
if ([string]::IsNullOrWhiteSpace($AppsettingsPath)) {
    $AppsettingsPath = Join-Path $raiz 'src\OilsMexico.Web\appsettings.json'
}
$defName = 'oilsmexico_erp'
$defUser = 'postgres'
$defHost = 'localhost'
$defPort = 5432
$defPass = ''
if (Test-Path $AppsettingsPath) {
    try {
        $cfg = Get-Content $AppsettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $cs = $cfg.ConnectionStrings.ErpDb
        if ($cs) {
            foreach ($par in ($cs -split ';')) {
                $kv = $par -split '=', 2
                if ($kv.Count -ne 2) { continue }
                $k = $kv[0].Trim().ToLowerInvariant()
                $v = $kv[1].Trim()
                switch ($k) {
                    'database' { if ($v) { $defName = $v } }
                    'username' { if ($v -and $v -ne 'CAMBIAR_AQUI') { $defUser = $v } }
                    'host'     { if ($v) { $defHost = $v } }
                    'port'     { $p = 0; if ([int]::TryParse($v, [ref]$p)) { $defPort = $p } }
                    'password' { if ($v -and $v -ne 'CAMBIAR_AQUI') { $defPass = $v } }
                }
            }
        }
    } catch { Write-Warning "No se pudo leer appsettings, uso defecto: $($_.Exception.Message)" }
}
if ([string]::IsNullOrWhiteSpace($DbName)) { $DbName = $defName }
if ([string]::IsNullOrWhiteSpace($DbUser)) { $DbUser = $defUser }
if ([string]::IsNullOrWhiteSpace($DbHost)) { $DbHost = $defHost }
if ($DbPort -le 0) { $DbPort = $defPort }
if ([string]::IsNullOrWhiteSpace($BackupDir)) { $BackupDir = Join-Path $raiz 'backups' }
function Find-PgDump {
    param([string]$Hint)
    if ($Hint) {
        $c = Join-Path $Hint 'pg_dump.exe'
        if (Test-Path $c) { return (Resolve-Path $c).Path }
        if (Test-Path $Hint -PathType Leaf) { return (Resolve-Path $Hint).Path }
    }
    $cmd = Get-Command 'pg_dump.exe' -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $base = 'C:\Program Files\PostgreSQL'
    if (Test-Path $base) {
        $f = Get-ChildItem $base -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'bin\pg_dump.exe' } |
            Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($f) { return $f }
    }
    return $null
}
$pgDump = Find-PgDump -Hint $PgBinPath
if (-not $pgDump) { throw "No se encontro pg_dump.exe. Instala PostgreSQL 18 o usa -PgBinPath 'C:\Program Files\PostgreSQL\18\bin'." }
Write-Host "pg_dump: $pgDump"
Write-Host "Destino: $DbUser@$DbHost`:$DbPort/$DbName -> $BackupDir"
$plainPass = ''
if ($DbPassword) { $plainPass = $DbPassword }
elseif ($env:PGPASSWORD) { $plainPass = $env:PGPASSWORD }
elseif ($defPass) { $plainPass = $defPass }
else {
    $sec = Read-Host "Password de PostgreSQL para '$DbUser'" -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try { $plainPass = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    if ([string]::IsNullOrEmpty($plainPass)) { throw 'Password vacio, backup cancelado.' }
}
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null }
$BackupDir = (Resolve-Path $BackupDir).Path
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$archivo = Join-Path $BackupDir ("{0}_{1}.dump" -f $DbName, $stamp)
$logFile = Join-Path $BackupDir 'backup.log'
function Write-Log {
    param([string]$Msg)
    $linea = "[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Msg
    $linea | Tee-Object -FilePath $logFile -Append | Write-Host
}
Write-Log "Iniciando backup de $DbName..."
$env:PGPASSWORD = $plainPass
try {
    & $pgDump -U $DbUser -h $DbHost -p $DbPort -d $DbName `
        --format=custom --compress=9 --blobs --verbose --file=$archivo
    if ($LASTEXITCODE -ne 0) {
        if (Test-Path $archivo) { Remove-Item $archivo -Force -ErrorAction SilentlyContinue }
        throw "pg_dump fallo con codigo $LASTEXITCODE. Revisa usuario/password/conexion."
    }
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    $plainPass = $null
}
$info = Get-Item $archivo -ErrorAction Stop
if ($info.Length -le 0) {
    Remove-Item $archivo -Force -ErrorAction SilentlyContinue
    throw 'El respaldo quedo vacio (0 bytes). Verifica disco y permisos.'
}
$mb = [math]::Round($info.Length / 1MB, 2)
Write-Log "OK: $($info.Name) ($mb MB)"
if ($RetainDays -gt 0) {
    $limite = (Get-Date).AddDays(-$RetainDays)
    $viejos = Get-ChildItem $BackupDir -Filter ("{0}_*.dump" -f $DbName) -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -ne $info.FullName -and $_.LastWriteTime -lt $limite }
    foreach ($v in $viejos) {
        Remove-Item $v.FullName -Force
        Write-Log "Rotado (>$RetainDays dias): $($v.Name)"
    }
} else { Write-Log 'Rotacion desactivada (RetainDays=0).' }
Write-Log "Backup completado: $($info.FullName)"
Write-Host ''
Write-Host 'Para restaurar:' -ForegroundColor Green
Write-Host "  `$env:PGPASSWORD='TU_PASSWORD'"
Write-Host "  pg_restore -U $DbUser -h $DbHost -p $DbPort -d $DbName --clean --if-exists '$($info.FullName)'"


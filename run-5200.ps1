# Levanta OilsMexico ERP en http://localhost:5200/
# Uso: powershell -ExecutionPolicy Bypass -File .\run-5200.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $raiz
$url = 'http://localhost:5200/'

# 1) Libera el puerto 5200 si quedó ocupado por una corrida anterior.
$pid5200 = Get-NetTCPConnection -LocalPort 5200 -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique
foreach ($id in $pid5200) {
    if ($id -ne $PID) { try { Stop-Process -Id $id -Force -ErrorAction Stop; Write-Host "Puerto 5200 liberado (PID $id)." } catch { } }
}

# 2) Compila antes de correr (falla rápido si algo rompió).
dotnet build --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "El build falló; revisa los errores de arriba." }

# 3) Corre el proyecto Web fijo en el puerto 5200 y abre el navegador.
Start-Process $url
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/OilsMexico.Web/OilsMexico.Web.csproj --urls $url

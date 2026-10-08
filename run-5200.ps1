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

# 3) Abre la pantalla de espera bonita (con spinner "Cargando... por favor espere").
#    Esa página detecta sola cuándo el servidor ya responde y redirige al ERP,
#    así ya no se ve el error "No se puede acceder a este sitio" del navegador.
$splash = Join-Path $raiz 'iniciando-erp.html'
Start-Process $splash
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/OilsMexico.Web/OilsMexico.Web.csproj --urls $url

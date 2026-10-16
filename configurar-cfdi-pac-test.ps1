# =============================================================================
# Script de configuración para CFDI / PAC modo TEST (Finkok)
# Proyecto: OilsMexico ERP
# Uso: Ejecuta este script en PowerShell desde la raíz del repositorio.
# =============================================================================
# ⚠️  SEGURIDAD: Este script NO envía tus credenciales a ningún lado.
#      Las escribe SOLO en tu user-secrets local (fuera del repositorio).
#      Nunca compartas el resultado de este script ni tus CSD/credenciales.
# =============================================================================

$ErrorActionPreference = "Stop"
$rutaProyecto = Get-Location
$rutaWeb = Join-Path $rutaProyecto "src/OilsMexico.Web"
$rutaCerts = Join-Path $rutaProyecto "certs"

Write-Host "=============================================================================" -ForegroundColor Cyan
Write-Host "  OilsMexico ERP — Configuración CFDI / PAC modo TEST (Finkok)" -ForegroundColor Cyan
Write-Host "=============================================================================" -ForegroundColor Cyan
Write-Host ""

# --- Paso 1: Verificar que estamos en la raíz del proyecto ---
if (-not (Test-Path (Join-Path $rutaProyecto "OilsMexico.slnx"))) {
    Write-Host "ERROR: No se encontró OilsMexico.slnx. Ejecuta este script desde la raíz del proyecto." -ForegroundColor Red
    exit 1
}
Write-Host "✔ Proyecto encontrado en: $rutaProyecto" -ForegroundColor Green

# --- Paso 2: Verificar/crear carpeta certs/ ---
if (-not (Test-Path $rutaCerts)) {
    Write-Host "Creando carpeta certs/ ..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $rutaCerts | Out-Null
}
Write-Host "✔ Carpeta certs/ lista: $rutaCerts" -ForegroundColor Green
Write-Host "  ⚠️  Coloca aquí tus archivos csd.cer y csd.key (nunca se suben al repo)." -ForegroundColor Yellow

# --- Paso 3: Inicializar user-secrets si no existe ---
Write-Host ""
Write-Host "Inicializando user-secrets para OilsMexico.Web ..." -ForegroundColor Cyan
& dotnet user-secrets init --project $rutaWeb 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  (ya estaba inicializado o se omite)" -ForegroundColor Gray
} else {
    Write-Host "  ✔ user-secrets inicializado" -ForegroundColor Green
}

# --- Paso 4: Solicitar credenciales de Finkok TEST (seguro, no se muestran) ---
Write-Host ""
Write-Host "Configuración de credenciales Finkok TEST" -ForegroundColor Cyan
Write-Host "-----------------------------------------" -ForegroundColor Cyan

$usuario = Read-Host "Ingresa tu usuario de Finkok TEST (Cfdi__Pac__Usuario)"
$password = Read-Host "Ingresa tu password de Finkok TEST (Cfdi__Pac__Password)" -AsSecureString
$csdPassword = Read-Host "Ingresa la contraseña de tu CSD (.key) si aplica, o déjalo vacío" -AsSecureString

# Convertir SecureString a texto plano para user-secrets (solo en memoria de este script)
$passwordTexto = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
$csdPasswordTexto = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($csdPassword))

# --- Paso 5: Escribir en user-secrets ---
Write-Host ""
Write-Host "Escribiendo en user-secrets ..." -ForegroundColor Cyan

& dotnet user-secrets set "Cfdi__Pac__Usuario" $usuario --project $rutaWeb
if ($LASTEXITCODE -ne 0) { Write-Host "  ✖ Error guardando Cfdi__Pac__Usuario" -ForegroundColor Red; exit 1 }

& dotnet user-secrets set "Cfdi__Pac__Password" $passwordTexto --project $rutaWeb
if ($LASTEXITCODE -ne 0) { Write-Host "  ✖ Error guardando Cfdi__Pac__Password" -ForegroundColor Red; exit 1 }

& dotnet user-secrets set "Cfdi:PacModo" "TEST" --project $rutaWeb
if ($LASTEXITCODE -ne 0) { Write-Host "  ✖ Error guardando Cfdi:PacModo" -ForegroundColor Red; exit 1 }

if ($csdPasswordTexto -and $csdPasswordTexto.Length -gt 0) {
    & dotnet user-secrets set "Cfdi:KeyPassword" $csdPasswordTexto --project $rutaWeb
    if ($LASTEXITCODE -ne 0) { Write-Host "  ✖ Error guardando Cfdi:KeyPassword" -ForegroundColor Red; exit 1 }
}

Write-Host "  ✔ Credenciales guardadas en user-secrets (fuera del repo)" -ForegroundColor Green

# --- Paso 6: Verificar configuración ---
Write-Host ""
Write-Host "Verificando configuración ..." -ForegroundColor Cyan
$modo = & dotnet user-secrets get "Cfdi:PacModo" --project $rutaWeb 2>$null
Write-Host "  Cfdi:PacModo = $modo" -ForegroundColor Green

# --- Paso 7: Build ---
Write-Host ""
Write-Host "Ejecutando dotnet build ..." -ForegroundColor Cyan
& dotnet build "OilsMexico.slnx" --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✖ El build falló. Revisa los errores arriba." -ForegroundColor Red
    exit 1
}
Write-Host "  ✔ Build correcto (0 errores)" -ForegroundColor Green

# --- Paso 8: Instrucciones finales ---
Write-Host ""
Write-Host "=============================================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURACIÓN COMPLETADA" -ForegroundColor Green
Write-Host "=============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Siguientes pasos (tú los ejecutas manualmente):" -ForegroundColor Yellow
Write-Host "  1. Coloca certs/csd.cer y certs/csd.key en la carpeta certs/ (si aún no lo has hecho)."
Write-Host "  2. Levanta PostgreSQL con el seed del ERP."
Write-Host "  3. Ejecuta la app: dotnet run --project src/OilsMexico.Web"
Write-Host "  4. Ve a http://localhost:5200/facturacion"
Write-Host "  5. Verifica que el badge muestre 'PAC Finkok: TEST' (no SIMULADO)."
Write-Host "  6. Timbra una factura PUE, una PPD + REP, cancela con motivo 02."
Write-Host ""
Write-Host "Recuerda: nunca subas certs/*.key, certs/*.cer ni credenciales al repositorio." -ForegroundColor Yellow
Write-Host ""

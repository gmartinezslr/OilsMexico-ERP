# OilsMexico ERP — Aceites y Lubricantes Automotrices (México)

ERP interno multi-sucursal con facturación electrónica **CFDI 4.0 nativa** (sin terceros, excepto timbrado PAC),
control de inventarios por lotes y POS en tiempo real.

## Stack
- **.NET 10** (C#) — Arquitectura en capas: `Domain` / `Application` / `Infrastructure` / `Web`
- **PostgreSQL 18** + EF Core (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Blazor Server** (InteractiveServer) + SignalR + Bootstrap 5

## Estructura
```
OilsMexico.slnx
src/
  OilsMexico.Domain/          # Entidades: Producto, UnidadMedida, Sucursal, Lotes, Inventario, Factura, Cliente, Usuario, Kardex
  OilsMexico.Application/     # DTOs, interfaces, sesión, catálogos SAT
  OilsMexico.Infrastructure/  # ErpDbContext, servicios (ventas/inventario/CFDI/PAC), seed
  OilsMexico.Web/             # Blazor: Login PIN, POS, Almacén, Ticket 58mm, Hub SignalR
```

## Reglas de negocio (inmutables)
1. **Multi-sucursal aislado:** todo se filtra por `SucursalId` de la sesión.
2. **CFDI nativo C#:** `System.Security.Cryptography`, CSD `.key`/`.cer`, cadena original SHA-256, timbrado vía PAC.
3. **Conversiones de volumen:** `litros = cantidad × factor_conversion`; descuento FIFO por caducidad.

## Requisitos
- .NET SDK 10, PostgreSQL 18, puerto 5200 libre.

## Puesta en marcha
```powershell
# 1. Crear BD
$env:PGPASSWORD='postgres'
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h localhost -c "CREATE DATABASE oilsmexico_erp;"

# 2. Configurar conexión (NO subir passwords reales al repo)
# Edita src/OilsMexico.Web/appsettings.json -> ConnectionStrings:ErpDb
# O usa user-secrets:
dotnet user-secrets init --project src/OilsMexico.Web
dotnet user-secrets set "ConnectionStrings:ErpDb" "Host=localhost;Port=5432;Database=oilsmexico_erp;Username=postgres;Password=TU_PASSWORD" --project src/OilsMexico.Web

# 3. Correr (el seed crea sucursales, productos, clientes y usuarios)
dotnet run --project src/OilsMexico.Web --urls "http://localhost:5200"
```
Abre `http://localhost:5200/` → Login → POS `/ventas-pos` → Almacén `/almacen` → Ticket `/ticket/{id}`.

## Accesos demo (PIN)
| PIN | Rol | Destino |
|-----|-----|---------|
| 1234 | Admin | POS |
| 1111 | Vendedor | POS |
| 2222 | Almacén | Almacén |
| 3333 | Conta | POS |

## Módulos
- **Login PIN** con sesión persistente (ProtectedSessionStorage).
- **POS:** búsqueda SKU/marca/viscosidad, carrito, IVA 16%, FormaPago/Método/UsoCfdi SAT, toggle CFDI, SignalR por sucursal.
- **Almacén:** entradas/compras, ajustes, traspasos, kardex auditado.
- **Ticket 58mm** imprimible (`window.print`).
- **CFDI:** sellado RSA-SHA256 + PAC (modo `SIMULADO` por defecto; configura `Cfdi:` en appsettings y `certs/` local, nunca al repo).

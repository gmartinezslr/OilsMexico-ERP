# OilsMexico ERP — Aceites y Lubricantes Automotrices (México)

Añade **Corte de caja** (apertura, fotografía del sistema, cuadrícula de contado y cierre), **NC electrónica** y **REP/cobro parcial** en el menú de Facturación/Historial.

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
  OilsMexico.Web/             # Blazor: Login PIN, POS, Historial, Facturación CFDI, Almacén, Viscosidad, Sucursal, Ticket 58mm, Hub SignalR
```

## Reglas de negocio (inmutables)
1. **Multi-sucursal aislado:** todo se filtra por `SucursalId` de la sesión.
2. **CFDI nativo C#:** `System.Security.Cryptography`, CSD `.key`/`.cer`, cadena original SHA-256, timbrado vía PAC.
3. **Conversiones de volumen:** `litros = cantidad × factor_conversion`; descuento FIFO por caducidad.

## Requisitos (instalar desde cero)
| Herramienta | Versión usada | Descarga |
|---|---|---|
| .NET SDK | 10.x (`10.0.112`+ verificado) | https://dotnet.microsoft.com/download |
| PostgreSQL | 18.x (servicio `postgresql-x64-18`) | https://www.postgresql.org/download/windows/ |
| Git | 2.50+ | https://git-scm.com/download/win |
| Editor | VS Code + extensión C# Dev Kit (opcional) | https://code.visualstudio.com/ |
| Navegador | Chrome/Edge actual | — |

> La app **crea sola las 12 tablas** al arrancar (`EnsureCreatedAsync` + `SchemaPatch` para BDs existentes) y las siembra con datos demo. El dev **no necesita scripts SQL ni migraciones**.

## Puesta en marcha (5 pasos, ~10 min)
```powershell
# 0. Clonar
git clone https://github.com/gmartinezslr/OilsMexico-ERP.git
cd OilsMexico-ERP

# 1. Crear BD vacía (solo el contenedor; las tablas las crea la app)
$env:PGPASSWORD='TU_PASSWORD_POSTGRES'
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h localhost -c "CREATE DATABASE oilsmexico_erp;"

# 2. Configurar conexión SIN tocar el repo (recomendado: user-secrets)
dotnet user-secrets init --project src/OilsMexico.Web
dotnet user-secrets set "ConnectionStrings:ErpDb" "Host=localhost;Port=5432;Database=oilsmexico_erp;Username=postgres;Password=TU_PASSWORD" --project src/OilsMexico.Web
# Alternativa rápida (solo local, NO commitear): editar
# src/OilsMexico.Web/appsettings.json -> ConnectionStrings:ErpDb -> Password=CAMBIAR_AQUI

# 3. Restaurar y compilar
dotnet build OilsMexico.slnx
# Esperado: "Compilación correcta. 0 Errores"

# 4. Correr (crea tablas + seed automático)
dotnet run --project src/OilsMexico.Web --urls "http://localhost:5200"
# Esperado en consola: "Now listening on: http://localhost:5200"

# 5. Abrir
http://localhost:5200/  (hub) -> /login -> /ventas-pos -> /historial-ventas -> /facturacion -> /corte-caja -> /notas-credito -> /complementos-pago
```
Abre `http://localhost:5200/` → Login → POS `/ventas-pos` → Historial `/historial-ventas` → Facturación `/facturacion` → Almacén `/almacen` → Ticket `/ticket/{id}`.

**Todas las rutas:** `/ventas-pos`, `/historial-ventas`, `/facturacion`, `/almacen`, `/viscosidad`, `/compras`, `/productos`, `/clientes`, `/proveedores`, `/sucursal`, `/usuarios`, `/sepomex`, `/ticket/{id}`.

## Accesos demo (PIN)
| PIN | Rol | Destino |
|-----|-----|---------|
| 1234 | Admin | POS |
| 1111 | Vendedor | POS |
| 2222 | Almacén | Almacén |
| 3333 | Conta | POS |

## Verificación (¿quedó bien?)
```powershell
# Tablas creadas (esperado: 12)
$env:PGPASSWORD='TU_PASSWORD'
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h localhost -d oilsmexico_erp -c "SELECT count(*) FROM pg_tables WHERE schemaname='public';"
# Sucursales + productos seed
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h localhost -d oilsmexico_erp -c "SELECT codigo_sucursal FROM sucursales; SELECT sku FROM productos;"
# Endpoints (esperado: 200)
curl.exe -s -o NUL -w 'login:%{http_code} ' http://localhost:5200/login
curl.exe -s -o NUL -w 'pos:%{http_code}' http://localhost:5200/ventas-pos
curl.exe -s -o NUL -w 'historial:%{http_code}' http://localhost:5200/historial-ventas
curl.exe -s -o NUL -w 'facturacion:%{http_code}' http://localhost:5200/facturacion
curl.exe -s -o NUL -w 'viscosidad:%{http_code}' http://localhost:5200/viscosidad
curl.exe -s -o NUL -w 'sucursal:%{http_code}' http://localhost:5200/sucursal
curl.exe -s -o NUL -w 'almacen:%{http_code}' http://localhost:5200/almacen
# XML de una factura (404 = endpoint activo, factura inexistente)
curl.exe -s -o NUL -w 'xml:%{http_code}' http://localhost:5200/api/facturas/1/xml
```

## Solución de problemas
| Síntoma | Causa | Fix |
|---|---|---|
| `fe_sendauth: no password supplied` | Falta `$env:PGPASSWORD` | Exportarlo antes de `psql` o usar pgAdmin |
| `database oilsmexico_erp does not exist` | No se creó la BD | Paso 1 (solo `CREATE DATABASE`, las tablas las crea la app) |
| Login no navega / página plana sin CSS | Caché del navegador | `Ctrl+Shift+R` (recarga dura) |
| `An unhandled error` en POS | Circuito caído | Reinicia `dotnet run`, revisa consola |
| Puerto 5200 ocupado | Otra instancia corriendo | `Get-Job \| Stop-Job` o cambia `--urls` |
| `Password=CAMBIAR_AQUI` | Falta user-secrets | Paso 2 (no edites el json trackeado) |

## Cómo está cableado (para el dev)
- `Program.cs` → `UseNpgsql(GetConnectionString("ErpDb"))` → `SeedData.InicializarAsync` (`EnsureCreated` + `SchemaPatch` idempotente + seed si `Sucursales` vacía).
- Tablas exactas del modelo: `productos, unidades_medida, sucursales, inventario_lotes, inventario_sucursal, facturas, factura_detalle, clientes, usuarios, movimientos_inventario, codigos_postales, proveedores, compras, compra_detalle, compra_pagos`.
- Seed: 2 sucursales (CDMX01/MTY01) con datos fiscales del emisor (razón social, régimen 601, CP y domicilio SEPOMEX), 4 lubricantes, unidades Litro×1/Garrafa×19/Tambor×208, 2 clientes, 4 usuarios PIN (SHA-256), stock 100L por producto/sucursal.
- CFDI sin CSD reales → modo `SIMULADO` (sello DEV auditable, solo facturas). Para timbrado real: carpeta `certs/` local con `csd.cer` + `csd.key` (nunca al repo), `Cfdi:PacModo = TEST | PRODUCCION` y credenciales Finkok en `Cfdi:Pac` (user-secrets: `Cfdi__Pac__Usuario`, `Cfdi__Pac__Password`).
- Columnas nuevas aplicadas por `SchemaPatch` (sin migraciones EF): dirección de clientes/proveedores, datos del emisor en `sucursales` (razón social, régimen, CP, domicilio, contacto) y `facturas.motivo_cancelacion`.
- API mínima: `GET /api/facturas/{id}/xml` descarga el CFDI (`application/xml`; 404 si no existe).

## Módulos
- **Login PIN** con sesión persistente (ProtectedSessionStorage).
- **POS:** búsqueda SKU/marca/viscosidad, carrito, IVA 16%, FormaPago/Método/UsoCfdi SAT, toggle CFDI, SignalR por sucursal.
- **Almacén:** entradas/compras, ajustes, traspasos, kardex auditado.
- **Datos de la sucursal (`/sucursal`):** nombre, código, RFC/razón social/régimen/CP del emisor CFDI, domicilio con combo SEPOMEX y contacto; edición **solo rol Admin** (demás roles vista de solo lectura).
- **Historial de ventas (`/historial-ventas`):** filtros por fecha/estado/folio-cliente, resumen (ventas, subtotal, IVA, total), paginación, detalle expandible con renglones + sello/cadena original, y acciones **Surtir** / **Devolver** con motivo (repone stock y deja rastro en kardex).
- **Aceites por viscosidad (`/viscosidad`):** tarjetas por viscosidad (litros, valor del stock y alerta de bajo mínimo), tabla con stock/mínimo/precio, búsqueda por SKU/nombre/marca en cliente y botón **Kardex** que abre `/almacen?producto={id}`.
- **Facturación CFDI (`/facturacion`):** listado de folios con estado fiscal y UUID, **timbrado diferido** (sellado RSA-SHA256 + PAC fuera del POS para ventas quedadas en Pendiente), **cancelación real** ante Finkok (`cancel_signature` firmada con el CSD; motivo SAT 01|02|03|04, 01 exige UUID sustituto; solo Admin/Conta) y **descarga de XML** (`/api/facturas/{id}/xml`).
- **Clientes / Proveedores / Productos / Usuarios:** CRUD (productos con presentaciones y stock por sucursal; usuarios solo-Admin con PIN SHA-256).
- **Compras (`/compras`):** ciclo orden → recepción parcial/total (suma stock + lote + kardex COMPRA) → CxP (pagos con forma SAT, estados Pagada/Parcial/Pendiente).
- **SEPOMEX (`/sepomex`):** importación del catálogo nacional de Correos de México — **Excel oficial `.xls` (una hoja por estado, detectado por firma OLE2/ZIP)** o TXT/CSV (15 columnas, `|`/`,`/`;`/TAB, latin1/UTF-8, dedup por `codigo|asentamiento_id|nombre`); el componente `DireccionSepomex` consulta el catálogo por CP para llenar el **combo obligatorio de colonias** (sin captura libre; si el CP tiene una sola colonia, ésta se preselecciona) y autocompletar municipio/ciudad/estado —que se muestran como etiquetas de solo lectura— en clientes y proveedores.
- **Ticket 58mm** imprimible (`window.print`).
- **CFDI:** sellado nativo RSA-SHA256 (CSD `.cer`/`.key` + XSLT oficial del SAT, recurso embebido) para facturas (I), notas de crédito (E con relación 01) y REP (P + Pagos 2.0); timbrado Finkok SOAP `stamp` en `TEST`/`PRODUCCION` (`SIMULADO` = UUID local auditable, solo facturas sin CSD); cancelación `cancel_signature` firmada localmente (la llave nunca viaja al PAC) + consulta `get_sat_status`. Nodos `Impuestos` con `Traslados/Traslado` (IVA `002`, tasa 16%) a nivel concepto y global.

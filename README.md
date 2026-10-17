# OilsMexico ERP — Aceites y Lubricantes Automotrices (México)

ERP web multi-sucursal para la operación de un negocio de aceites y lubricantes automotrices en México. Incluye ventas, inventario, compras, CFDI, cobranza, contabilidad, reportes y administración desde una aplicación Blazor Server.

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

> Al iniciar, la aplicación prepara el esquema con EF Core, aplica los parches idempotentes para bases existentes y carga los datos iniciales cuando corresponde. El modelo evoluciona junto con el código; no asumas un número fijo de tablas.

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
http://localhost:5200/ -> /login -> /ventas-pos
```
Inicia sesión y abre los módulos autorizados para tu rol desde el menú principal.

**Rutas principales:** `/`, `/login`, `/ventas-pos`, `/historial-ventas`, `/cotizaciones`, `/cotizaciones/nueva`, `/ordenes-vente`, `/facturacion`, `/notas-credito`, `/complementos-pago`, `/corte-caja`, `/compras`, `/diferenciadores`, `/gastos`, `/estado-cuentas`, `/bancos`, `/reportes`, `/metas`, `/productos`, `/almacen`, `/viscosidad`, `/clientes`, `/proveedores`, `/sepomex`, `/sucursal`, `/usuarios`, `/configuracion`, `/permisos`, `/auditoria`, `/comunicados`, `/ticket/{FacturaId}`. Algunas pantallas de detalle usan rutas parametrizadas; el menú y los permisos determinan cuáles aparecen para cada usuario.

## Accesos demo (PIN)
| PIN | Rol | Destino |
|-----|-----|---------|
| 1234 | Admin | POS |
| 1111 | Vendedor | POS |
| 2222 | Almacén | Almacén |
| 3333 | Conta | POS |

## Verificación (¿quedó bien?)
```powershell
# Comprobar que la aplicación creó tablas en el esquema public
$env:PGPASSWORD='TU_PASSWORD'
& 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h localhost -d oilsmexico_erp -c "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;"
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
- El esquema se define con entidades y configuraciones de EF Core en `OilsMexico.Domain` y `OilsMexico.Infrastructure/Persistence`; las migraciones y parches idempotentes permiten evolucionar las bases existentes.
- Seed: 2 sucursales (CDMX01/MTY01) con datos fiscales del emisor (razón social, régimen 601, CP y domicilio SEPOMEX), 4 lubricantes, unidades Litro×1/Garrafa×19/Tambor×208, 2 clientes, 4 usuarios PIN (SHA-256), stock 100L por producto/sucursal.
- CFDI sin CSD reales → modo `SIMULADO` (sello DEV auditable, solo facturas). Para timbrado real: carpeta `certs/` local con `csd.cer` + `csd.key` (nunca al repo), `Cfdi:PacModo = TEST | PRODUCCION` y credenciales Finkok en `Cfdi:Pac` (user-secrets: `Cfdi__Pac__Usuario`, `Cfdi__Pac__Password`).
- Los endpoints de descarga de XML y de exportación están mapeados en `src/OilsMexico.Web/Program.cs`; las descargas requieren una sesión válida.

## Módulos
- **Inicio y seguridad:** panel principal, autenticación, gestión de sesiones, política de contraseñas y autenticación de dos factores.
- **Ventas:** POS, historial, cotizaciones y órdenes de venta; cotización → orden → factura; límites de crédito y validaciones de venta.
- **Facturación electrónica:** CFDI 4.0, notas de crédito y complementos de pago; integración PAC en modos SIMULADO/TEST/PRODUCCIÓN, sujeto a la configuración y credenciales correspondientes.
- **Caja y cobranza:** apertura/cierre y corte de caja, gastos operativos, CxC/CxP, vencimientos, pagos y estados de cuenta.
- **Compras y proveedores:** órdenes de compra, recepción, pagos y seguimiento de cuentas por pagar.
- **Productos e inventario:** catálogo, existencias por sucursal, ajustes, traspasos, kardex, códigos de barras y aceites por viscosidad.
- **Diferenciadores (`/diferenciadores`):** listas de precio menudeo/mayoreo con umbrales por litros equivalentes integrados al POS/cotizaciones; catálogo de compatibilidad por vehículo consultable desde ventas; evaluación por recepción de proveedor; reparto/liquidación sin una segunda salida de inventario; control físico de tambor y envases retornables; captura y XML preliminar Carta Porte 3.1. La Carta Porte generada es un borrador operativo: no se valida contra los XSD/catálogos completos del SAT ni se sella/timbra. Devoluciones de mercancía facturada requieren nota de crédito; depósitos de envases son informativos, no asientos ni cobros.
- **Clientes y proveedores:** administración de terceros, datos fiscales y múltiples contactos, correos, teléfonos y direcciones.
- **Bancos:** cuentas, movimientos y conciliación bancaria.
- **Contabilidad y reportes:** asientos, FIFO para costo de ventas, cierre/reapertura de periodos y reportes financieros (balanza, balance, resultados, existencias e indicadores).
- **Comisiones:** metas, cálculo, consulta y cierre de periodos.
- **Administración:** sucursales, usuarios, configuración, permisos por rol, auditoría, catálogos SEPOMEX y comunicados.
- **Exportación y documentos:** exportaciones Excel/PDF para los reportes disponibles, PDF de CFDI con QR y ticket imprimible.

Las páginas se implementan en `src/OilsMexico.Web/Components/Pages/ERP`. El POS admite escaneo de productos; inventario conserva movimientos de kardex y las operaciones se aíslan por sucursal. CFDI incluye sellado nativo, timbrado diferido mediante PAC, cancelación y consulta de estado SAT; el modo real requiere CSD y credenciales configuradas localmente.

La app también expone rutas parametrizadas para detalle de cotización, edición de cotización, detalle de orden y ticket. La autorización depende de la sesión, el rol y los permisos configurados; que una ruta exista no significa que esté disponible para todos los perfiles.

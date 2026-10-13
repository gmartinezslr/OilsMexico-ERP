# Situación al 11 de octubre de 2026 — Actualización del estatus del proyecto

Conserva intacto `Situación al 09 de octubre.md`. Este corte incorpora 4 commits posteriores al 09 (`e458332` → `050b76a`) verificados en código.

Commits cubiertos: `e458332 comisiones` (09/10) · `cad99a1 hardcode fases 1-6` (10/10) · `868948c hardcode completo SOLID` (11/10) · `050b76a barras y escaneo` (11/10).

## Resumen ejecutivo

**OilsMexico ERP** (.NET 10 + PostgreSQL + EF Core + Blazor Server + SignalR) mantiene el núcleo descrito al 09 y **cierra 3 faltantes** de aquel informe:

- ✅ **Validación RFC + Situación Fiscal** (era 🔴 \#5): `RfcValidador.cs` con algoritmo oficial SAT Anexo 3 (estructura, fecha, palabras prohibidas, dígito módulo 11), enum `SituacionFiscal` (6 estados), `Cliente.SituacionFiscal` / `Proveedor.SituacionFiscal`, validación en vivo y bloqueo con Cancelada/Baja.

- ✅ **Contraseña + 2FA** (era 🟢 \#10): `AuthService` con TOTP/QR (`OtpNet`), `PasswordHistory`, `AuditLog`, política configurable (longitud, historial, vigencia), `Login.razor` con reto 2FA + cambio obligatorio por vigencia, sección 2FA en Usuarios y tarjeta en Configuración.

- ✅ **Barras / escaneo** (era 🟢 \#2/\#4): `CodigosBarra.cs` (normalizar + `PareceEscaneo` 8-14 dígitos), búsqueda POS por código (`VentasService.Buscar.cs`), alta directa al carrito, validación en Productos.

- 🟡 **Ayuda / avisos** (era 🟢 \#11, parcial): `AyudaContextual.razor` (botón "?") + `AvisosCampana.razor` + `AvisosService` (ventas cobradas, códigos no encontrados, clientes creados). Falta wizard de alta y búsqueda global.

- ✅ **Ayuda / búsqueda / plantillas — HECHO 11/10 (tarde)**: wizard 3 pasos en Clientes **y** Proveedores (Datos → Facturación → Dirección SEPOMEX), `BusquedaGlobal` en header (Ctrl+K visual, debounce 250ms, top 8 por módulo: clientes/productos/proveedores/ventas/compras, respeta sucursal), `PlantillasMensaje` en Cuentas por Cobrar (cobro WhatsApp/correo, entrega, bienvenida, ticket; variables \{Nombre\}\{Folio\}\{Total\}\{Fecha\}\{Empresa\}), `IBusquedaGlobalService` + `BusquedaGlobalService`, ayuda contextual en Proveedores y EstadoCuentas.

- ✅ **Comisiones ampliadas**: `CuotaVendedor` (dinero + litros, operador Y/O/Solo, `CeroComision|PagoMinimo`), `ComisionHistorial` congelado (Borrador/Cerrado/Pagado, desglose JSON), `ComisionesService.\*` + `CobranzaService.\*`, página `Metas.razor(.cs)`, `Cliente.VendedorId` + `Factura.VendedorId` para atribución.

- ✅ **Deuda técnica hardcode**: `Roles.cs` (`Roles.Admin`), `CfdiDefaults.cs`, `Impuestos` (fuente única IVA), cliente POS por RFC `XAXX010101000` (`RfcValidador.RfcPublicoGeneral`), `Importacion:RutaCatalogo` en `appsettings.json`, `ValidarModo` acepta SIMULADO, tablas `Roles/Permisos/RolPermisos/ReglasAsiento` en `SchemaPatch`.

- ✅ **Conciliación bancaria — HECHO 12/10**: entidades `CuentaBancaria` + `MovimientoBancario` (extracto), enums `NaturalezaMovimiento` (Abono/Cargo) y `EstadoConciliacion` (Pendiente/Conciliado), tablas `cuentas_bancarias`/`movimientos_bancarios` en `BancosPatch` (CHECK de enums vía `Enum.GetNames`), `BancosConfigs` (EF snake_case), `IBancosService` + `BancosService(.Conciliacion)` y página `Bancos.razor`. Cierra el hueco «`CompraPago`/`VentaCobro` registran pago pero no la operación bancaria»: se captura el extracto del banco y cada renglón se **empata** con un `VentaCobro` (Abono) o un `CompraPago` (Cargo) con regla de naturaleza y 1-a-1; saldo = inicial + Σ abonos − Σ cargos. Verificado con prueba de integración contra PostgreSQL (15/15). Build 0 errores.

- ✅ **Cotizaciones / Órdenes de venta / facturación (era 🔴 \#1) — HECHO 12/10**: pipeline completo cotización → orden de venta → factura. **Capa A**: entidades `Cotizacion(Detalle)` + `OrdenVenta(Detalle)`, enums `EstadoCotizacion`/`EstadoOrdenVenta`, `CotizacionesService.*` + `CotizacionesPatch`, páginas `Cotizaciones`/`CotizacionDetalle`/`CotizacionForm`/`OrdenesVenta`/`OrdenesVentaDetalle`. **Capa B**: `ConvertirOrdenAVentaAsync` factura la orden Confirmada con precios de compromiso congelados (`VentaPosRequest.CongelarPrecios` desactiva el override de mayoreo), liga `FacturaId`, estado `Facturada` + guards (confirmar/cancelar/facturar rechazan estados inválidos). **Capa C**: `ExpirarCotizacionesAsync` al cargar el listado de cotizaciones (por sucursal de sesión, idempotente) y acciones **Suspender** (Confirmada → Suspendida, no facturable) y **Reanudar** (Suspendida → Confirmada) en la OV, en listado y detalle. Fix derivado de la prueba: `OrdenVenta.CotizacionId` ahora `int?` (null = manual); antes se insertaba `0` y la FK `cotizacion_id → cotizaciones` rechazaba toda orden manual. Verificado con prueba de integración en PostgreSQL (4/4 Capa C y regresión Capa B; 9/9 Capa B en la sesión previa). Build 0 errores.

Siguen 🔴 sin cambio: `Gasto` + `/gastos`, vencimientos/mora CxC-CxP, costo por lote FIFO en asientos, cierre de periodo automático, balance/resultados, exportación Excel/PDF, evaluación de proveedores.




## 1. Lo que ya existe y está operativo (actualizado al 11)

| Área | Estado al 11 |
| - | - |
| Seguridad | Login correo+contraseña (`PasswordHasher`), PIN legacy migrado, bloqueo 3 intentos → 30 min → definitivo, mensajes unificados, token revocable, `ProtectedSessionStorage` revalidado. **Nuevo:** 2FA TOTP con QR + clave manual, vigencia configurable + cambio obligatorio, historial que impide repetir, auditoría `PasswordChangeAdmin`. |
| Multi-sucursal | Sin cambio al 09 + helper de sucursal centralizado y constantes `Roles` (adiós literal `"Admin"` disperso). |
| Ventas POS | Sin cambio + **escaneo de barras**: si el texto parece escaneo (8-14 dígitos) va directo al carrito; normaliza espacios/guiones; aviso en campana si no se encuentra. |
| Historial | Sin cambio (filtros, Surtir/Devolver, kardex). |
| Almacén | Sin cambio (entradas/ajustes/traspasos, kardex, lotes, FIFO). |
| CFDI 4.0 | Sin cambio funcional + defaults unificados `CfdiDefaults` y tasa vía `Impuestos.TasaIvaSat`. |
| Compras/CxP | Sin cambio (orden → recepción → CxP). Sigue sin página de vencimientos. |
| Clientes/Proveedores | CRUD + SEPOMEX. **Nuevo:** `SituacionFiscal` (Actual/Adeudada/Inactiva/Cancelada/Suspendida/Baja), validación RFC en vivo (✓/✗ tipo+fecha) y al guardar; bloquea Cancelada/Baja. |
| Comisiones | **Ampliado:** cuota doble (dinero SIN IVA + litros), `OperadorLogico`, `AccionIncumplimiento`, tabulador por presentación (`UnidadMedida` +14), cash-basis con prorrateo, potencial por cobrar, portal vendedor (run-rate), cierre congelado + reabrir, histórico auditable. |
| Contabilidad | Sin cambio (asientos venta/compra/devolución, mayor, saldos) + reglas en tabla `ReglasAsiento`. Sigue sin costo por lote ni cierre automático. |
| Reportes | Sin cambio (ventas/día/producto/marca, ABC con umbrales en `configuracion`, cortes, viscosidad). Sin balance/resultados ni exportación. |
| Caja | Sin cambio (apertura/foto/cuadre/cierre/historial). |
| Bancos | **Nuevo 12/10:** `CuentaBancaria` + extracto `MovimientoBancario` (Abono/Cargo), conciliación contra `VentaCobro`/`CompraPago` con regla de naturaleza y 1-a-1, deshacer, saldo calculado y pendientes por cuenta. Página `Bancos.razor` (Gestión, Admin/Conta). |
| Utilidades | SEPOMEX, ticket 58mm, datos sucursal, timeout. **Hecho 11/10:** `AyudaContextual` + campana `Avisos` + `BusquedaGlobal` (Ctrl+K) + `PlantillasMensaje` + wizard proveedores. |



## 2. Faltantes re-etiquetados al 11 (evidencia)

### 🔴 Alta prioridad — siguen bloqueantes

1. **Cotizaciones y órdenes de venta (`Cotizacion` / `OrdenVenta`)** — 🔴 SIN CAMBIO. Búsqueda en código: cero resultados fuera del informe del 09. No existe entidad, página ni API.

2. **Gastos operativos (`Gasto`, `/gastos`)** — 🔴 SIN CAMBIO. Sin entidad ni página; caja sigue sin conciliar gasolina/taller/servicios.

3. **CxC / CxP con vencimientos** — 🔴 SIN CAMBIO. `EstadoCuentasService` y `ComprasService.CuentasPorPagarAsync` existen pero sin plazos/mora/corte CFDI crédito ni página de vencimientos.

4. **Costo de ventas por lote FIFO en asientos** — 🔴 SIN CAMBIO. `PrecioCosto` + `IAsientoGeneradorService` existen; no hay carga de costo por lote al cierre.

5. **Cierre de periodo contable** — 🔴 SIN CAMBIO. Sin botón de cierre ni pólizas automáticas (inventario/depreciación/acumulados).

### 🟡 Media — control y cumplimiento

1. **Conciliación bancaria** — ✅ HECHO 12/10. `CuentaBancaria` + `MovimientoBancario` (extracto), `BancosPatch`/`BancosConfigs`, `IBancosService` + `BancosService`, página `Bancos.razor` (grupo Gestión, Admin/Conta). Cada renglón del extracto se empata con `VentaCobro` (Abono) o `CompraPago` (Cargo) con regla de naturaleza y 1-a-1; permite deshacer y muestra saldo calculado y pendientes de ambos lados. Verificado con prueba de integración en PostgreSQL (15/15). Build 0 errores.

2. **Reportes financieros** — 🟡 SIN CAMBIO. Sin balance, resultados, cambios de existencias ni indicadores por sucursal/periodo.

3. **Exportación Excel/PDF** — 🟡 SIN CAMBIO. Solo `ExcelDataReader` (lectura SEPOMEX) + `window.print` del ticket.

4. **Auditoría y permisos granulares** — 🟡 PARCIAL (avanza, no cierra). Hecho: `AuditLog` (PasswordChange, DosFa\*, RfcValidated, FiscalSituationChanged, PasswordPolicyChanged), `RoleId` + tablas `Roles/Permisos/RolPermisos`, `Roles.Admin` centralizado. Falta: matriz por menú/recurso, quién aprueba qué, auditoría de almacén/compras/config general.

### 🟢 Baja / oportunidades

1. **Contraseña/2FA** — ✅ HECHO (ver Resumen). Pendiente solo prueba manual en runtime con app autenticadora + BD levantada. Recuperación por correo: `AuthService` ya notifica; confirmar flujo completo.

2. **Ayuda / notificaciones** — ✅ HECHO 11/10 (tarde). Wizard 3 pasos en Clientes y Proveedores; búsqueda global Ctrl+K (top 8 por módulo, respeta sucursal); 5 plantillas (cobro WA/correo, entrega, bienvenida, ticket) en Cuentas por Cobrar; ayuda contextual en Proveedores y EstadoCuentas. Build 0 errores.

3. **Barras productos** — ✅ HECHO (ver Resumen).


## 3. Roadmap v11 (misma tabla del 09 + columna Estado)

| Orden | Prioridad | Funcionalidad | Estado al 11 | Justificación |
| - | - | - | - | - |
| 1 | 🔴 | Cotizaciones/OV (SO/CT) | ✅ Hecho 12/10 | Precio comprometido, preventa |
| 2 | 🔴 | Gastos operativos | Pendiente | Conciliar caja real |
| 3 | 🔴 | CxC/CxP vencimientos | Pendiente | Riesgo crédito/financiero |
| 4 | 🔴 | Costo + cierre periodo | Pendiente | Resultados reales |
| 5 | 🔴 | RFC/situación fiscal | ✅ Hecho 10/10 | SAT — cerrar |
| 6 | 🟡 | Conciliación bancaria | ✅ Hecho 12/10 | Efectivo/proveedores |
| 7 | 🟡 | Reportes conta/cotización | Pendiente | Balance por sucursal |
| 8 | 🟡 | Exportar Excel/PDF | Pendiente | Operativa/entregas |
| 9 | 🟡 | Auditoría + permisos | 🟡 Parcial | Trazabilidad |
| 10 | 🟢 | Contraseña/2FA | ✅ Hecho 10/10 | Seguridad |
| 11 | 🟢 | Ayuda/búsqueda/plantillas | ✅ Hecho 11/10 | Usabilidad |


Sugerido próximo: 1 → 2 → 3 (mismo orden del 09; lo cerrado no se re-trabaja).

## 4. Verificación pendiente (manual, con BD)

- `dotnet build OilsMexico.slnx` → 0 errores (verificado en sesión previa; revalidar tras este archivo que no toca código).

- Login 2FA con app autenticadora + cambio por vigencia vencida.

- Alta/edición cliente/proveedor: RFC inválido, duplicado, Cancelada/Baja (debe bloquear).

- Escaneo en POS (USB como teclado) + código inexistente (debe avisar en campana).

- Cierre/reapertura de comisión en `Metas` y portal vendedor.

## 5. Higiene git al 11

- `Situación al 09 de octubre.md`: untracked (`??`), se conserva intacto — no tocar.

- Este archivo nuevo también quedará untracked hasta que decidas `add/commit`.

- Modificado sin versionar: `hardcode movible a tablas y mejoras SOLID.docx` (binario).

- Nota auditoría interna: el commit dice "hardcode completo", pero su propio `.md` lista A4/B8/A3 como verificación pendiente (81 literales `"Admin"` históricos → hoy `Roles.Admin`; 15 resoluciones de sucursal → helper; catálogos SAT en servicio, no en tabla + literales en `Compras.razor/Facturacion/NotasCredito`). No se declara 100% limpio/SOLID; queda como deuda explícita, no como hecho.


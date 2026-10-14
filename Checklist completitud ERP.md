# Checklist de completitud del ERP — 13 de octubre de 2026

> Lista maestra de **cada función que falta o necesita modificación/mejora** para que OilsMexico ERP quede completo.
> Cada ítem fue **verificado contra el código** (build `OilsMexico.slnx` 0 errores; Cotizaciones Capa A+B+C probadas 5/5 en PostgreSQL desechable). Última actualización **13/10/2026** (rev. 3): §1.2 implementado y compilado; quedan pendientes pruebas integrales de BD/SMTP y bloqueo de crédito (depende de completar UI/política de crédito en §4.1).
> Leyenda: 🔴 alta prioridad · 🟡 media · 🟢 baja · `- [ ]` pendiente · `- [x]` hecho (sección 9, no re-trabajar).

**Fuentes:** roadmap v11 (`Situación al 11 de octubre.md`), auditoría Gemini (`Gemini sugerencias sobre el estado y mejoras.md`), revisión hardcode/SOLID (`hardcode movible a tablas y mejoras SOLID.md`), auditoría SOLID de Cotizaciones (sesión 12/10) y verificación directa sobre el código hoy.

**Mapa:** 1 Funcionalidades nuevas · 2 Exportación/CFDI · 3 Auditoría/permisos · 4 Comercial/crédito · 5 Logística · 6 Deuda técnica · 7 Verificación manual · 8 Documentación · 9 Hecho · 10 Fuera de alcance.

---

## 1. 🔴 Funcionalidades nuevas — bloqueantes

### 1.1 Gastos operativos (`/gastos`) — roadmap #2 ✅ HECHO (13/10/2026)

**Verificado contra código — todos los ítems ya estaban implementados (build 0 errores).**

- [x] **Entidad `Gasto`** en `Domain/Entities/Gasto.cs` (fecha, categoría, importe, forma de pago SAT, sucursal, usuario, comprobante, `CorteCajaId?`, notas) + `GastoConfig` (snake_case, tabla `gastos`, índices por sucursal/fecha y categoría) + `GastosPatch` idempotente con CREATE TABLE, FK a `cortes_caja` ON DELETE SET NULL, CHECK de categorías derivado de `Enum.GetNames<CategoriaGasto>()` y CHECK importe > 0.
- [x] **`IGastoService` + `GastoService`**: `CrearAsync`, `EditarAsync`, `EliminarAsync` (guard REGLA #1 via `ResolverSucursalAutorizada` + guard de sucursal en edición/borrado), `ListarAsync(fechas, categoría)`, `TotalesPorCategoriaAsync`. Registrado en `Program.cs` (`AddScoped<IGastoService, GastoService>()`).
- [x] **Página `/gastos`** (`Gastos.razor` + `Gastos.razor.cs`): listado con filtros fecha/categoría, formulario alta/edición en línea, tabla de totales por categoría, guard de rol (Admin/Conta/Vendedor), carga perezosa. `NavLink` en `NavMenu.razor` grupo Gestión + mapeo `ActualizarGrupoPorUrl`.
- [x] **Vinculación con `CorteCajaService.Cierre.cs`**: `GastosEfectivoTurnoAsync` descuenta del cuadre los gastos en efectivo (forma `01`) del turno abierto; la `Diferencia` del corte ya refleja los egresos de caja.
- [x] **Asiento contable automático** (`GastoService.Poliza.cs`): `RegistrarPolizaAsync` genera póliza con `ReglasAsiento` evento `"Gasto"` vía `ICuentasContablesService.RegistrarAsientoAsync`; fallback a cuentas 6000↔1000/1100 si no hay reglas sembradas. `IAsientoGeneradorService.GenerarAsientosDeGastoAsync` también disponible.
- [x] **Reporte de gastos por período / sucursal / categoría** — en la página `/gastos` (tabla inferior) y endpoint de exportación Excel `/api/export/gastos/{sucursalId}` (vía `IExportacionService.GastosAsync`, ClosedXML).

### 1.2 CxC y CxP con vencimientos — roadmap #3 ✅ IMPLEMENTADO (13/10/2026)

- [x] **Antigüedad de saldos**: CxC en `EstadoCuentasService.Antiguedad.cs` y CxP en `ComprasService.Antiguedad.cs`, rangos 0-15 / 16-30 / 31-60 / +60 días por cliente/proveedor, saldos parciales netos de pagos y sucursal autorizada. CxC respeta PUE como contado y usa `Cliente.DiasCredito`; CxP usa la fecha específica de compra.
- [x] **Intereses moratorios configurables**: claves `cxc_tasa_moratoria_diaria` y `cxp_tasa_moratoria_diaria`, persistidas en `configuracion`, ambas editables por Admin en Configuración y cálculo simple por días de atraso. Tasa inicial 0 (desactivada).
- [x] **Fechas compromiso**: CxP guarda `Compra.FechaVencimiento`, capturable al crear orden; si no se indica aplica `Proveedor.DiasCredito` (default 30). `SchemaPatch` agrega columnas de forma idempotente y backfillea compras existentes. El cliente tiene `DiasCredito` (default 0); la fecha CxC se deriva de emisión + plazo del cliente.
- [x] **UI y avisos**: reporte detallado en `EstadoCuentas.razor` y antigüedad CxP en `Compras.razor`; al consultar, la campana `AvisosService` muestra total en mora mayor a 30 días.
- [ ] **Bloqueo por morosidad** en POS/Facturación cuando el saldo vencido supere el límite — depende de configurar y administrar `Cliente.LimiteCredito` desde UI en §4.1.
- [ ] **Verificación con BD**: build verificado; pendiente prueba integral de CxC/CxP con PostgreSQL para backfill de esquema, vencimientos, pagos parciales y cálculo de tasas.

### 1.3 Costo de ventas FIFO + cierre de período — roadmap #4

- [ ] **Asiento de costo de venta** en `AsientoGeneradorService.cs`: hoy no genera (Costo de Ventas vs. Almacén) con el costo real del lote salido por FIFO (`InventarioLote` ya calcula el costo).
- [ ] **Cierre contable mensual**: bandera de periodo cerrado + bloqueo de escritura de asientos en periodos cerrados (y reapertura solo Admin).
- [ ] **Estado de Resultados del periodo** alimentado por esos asientos (ver 1.4).

### 1.4 Reportes financieros — roadmap #7

**Servicio + UI implementados al 13/10/2026** (`IReportesFinancierosService` + `ReportesFinancierosService` parcial `.cs`/`.Parte2.cs` + página `/reportes` con tabs, build 0 errores, registrado en `Program.cs`). Fuente: `CuentasContables` / `AsientoContable` / `DetalleAsiento` / `Facturas` / kardex (`Movimientos`) existentes. Todo respeta REGLA #1 vía `ResolverSucursalAutorizada`.

- [x] **Balanza de Comprobación** por periodo y sucursal (débitos/créditos + saldo deudor/acreedor por cuenta).
- [x] **Balance General** al corte (Activo = Pasivo + Patrimonio, con corte UTC correcto + indicador "cuadrado/descuadrado").
- [x] **Estado de Resultados por sucursal y periodo** (ventas netas, costo de ventas por `Producto.PrecioCosto`, utilidad bruta, IVA trasladado, márgenes).
- [x] **Informe de cambio de existencias** (inicial derivado + compras − ventas ± ajustes ± traspasos/devoluciones, por producto).
- [x] **Indicadores por sucursal y periodo** (margen, rotación de inventario, días de inventario, ticket promedio, nº ventas, inventario promedio). Con "Todas" (null) un Admin ve todas las sucursales activas; el resto la suya (REGLA #1).
- [x] **Página `/reportes` consumiendo el servicio** (carga perezosa por pestaña, `??=` para no recalcular): 5 pestañas (Balanza/Balance/Resultados/Existencias/Indicadores) con tablas Bootstrap + `NavLink` "Reportes Financieros" en menú Gestión; filtros sucursal/desde/hasta/corte y guard de rol `Admin`/`Conta`.

**Pendiente (refinamiento):**
- [ ] **Costo de ventas FIFO real** en Estado de Resultados (hoy usa `Producto.PrecioCosto` del catálogo, no el costo del lote salido) — depende de 1.3.
- [ ] **Exportación Excel/PDF** de los 5 reportes (`IExportacionService.FinancieroPdfAsync` ya está declarada en la interfaz, falta implementación) — ver 2.1.
- [ ] **Verificación con BD** de los 5 reportes (ver 7).

### 1.4b (nuevo) Estado de Resultados vs. asientos contables

- [ ] Unificar la fuente: el Estado de Resultados del servicio calcula desde `Facturas`/`Producto`, mientras el Balance usa `AsientoContable`. Con el cierre de período y asiento de costo de venta (1.3) ambos deberían cuadrar por cuenta.

---


## 2. 🟡 Exportación y documentos

### 2.1 Exportación Excel/PDF — roadmap #8 ✅ HECHO (13/10/2026)

`IExportacionService` + `ExportacionService` (parcial, en `OilsMexico.Infrastructure`) con **12 métodos**: 9 Excel (ClosedXML) + 4 PDF (QuestPDF). Todo respeta REGLA #1 vía `ResolverSucursalAutorizada`. Build `OilsMexico.slnx` 0 errores. Paquetes `ClosedXML 0.105.1` + `QuestPDF 2025.7.1` (licencia Community configurada en `Program.cs`).

- [x] Paquetes `ClosedXML` (Excel) + `QuestPDF` (PDF) referenciados.
- [x] **`ExportacionService`**: `EstadoCuentaClienteAsync`, `CxPendientesAsync`, `CxPPendientesAsync`, `InventarioAsync`, `KardexAsync`, `VentasAsync`, `ComisionesAsync`, `ListaPreciosAsync`, `GastosAsync` (Excel).
- [x] **PDF de factura CFDI con QR SAT** (`QRCoder`, URL de verificación SAT) — representación impresa formal.
- [x] **PDF de cotización y orden de venta** (renglones + totales + vigencia).
- [x] **`FinancieroPdfAsync`**: PDF de balanza / balance / resultados / existencias (§1.4).
- [x] **Endpoints de descarga** `/api/export/*` (Minimal API, con guard de token de sesión, igual que la descarga de XML).
- [x] **Botones de exportación** en `/reportes` (barra Excel general + botón PDF por pestaña), con token `?t=` de sesión.

- [x] **Verificación con BD** — proyecto `tests/OilsMexico.IntegrationTests` (xUnit + EF InMemory): 10/10 pruebas passing validan que cada Excel empieza con "PK" y cada PDF con "%PDF", cubren los 4 financieros (§1.4), cotización/orden y REGLA #1. Detectó y corrigió un desborde de tabla en PDFs anchos (existencias).
- [x] **Adjuntar PDF del CFDI al correo** — `EnviarCfdiAsync` ahora adjunta XML **+ PDF** (vía `IExportacionService.FacturaCfdiPdfAsync`, inyectado en `EmailService`); si falla el PDF se envía igual el XML (defensivo, no rompe el timbrado). Aplica también al envío automático al timbrar.
- [x] **Botones de exportación/PDF en las demás páginas** — Facturación (PDF con QR), CotizacionDetalle (PDF), Compras (CxP Excel), Almacén (Inventario + Kardex Excel). Todos con token `?t=` de sesión.

**Pendiente (refinamiento menor):**
- [ ] Verificación manual con SMTP real (adjunto PDF llega correctamente) y con PostgreSQL productivo (los tests usan EF InMemory).

### 2.2 Envío automático de CFDI por correo ✅ HECHO (13/10/2026)

Remitente configurable en Configuración (tabla `configuracion`, claves `correo_*`, respaldo en
`appsettings` sección `Correo`): servidor, puerto, SSL, usuario, password, **remitente**,
nombre del remitente y bandera de envío automático. Build `OilsMexico.slnx` 0 errores.

- [x] **Paquete `MailKit`** (SMTP) en `OilsMexico.Infrastructure` + `IEmailService` y `EmailService`
  (+ `.Documentos.cs` + `.Masivos.cs`): config, prueba, `EnviarAsync`, cotizaciones, CFDI (XML adjunto),
  estados de cuenta CxC/CxP, comunicados masivos y recordatorios de saldo.
- [x] **Hook automático tras `TimbrarAsync`** (`EnviarCfdiAutomaticoAsync`: no rompe el timbrado si falla)
  + botón manual en `Facturacion.razor`; envío de cotización en `CotizacionDetalle.razor`.
- [x] **UI remitente**: componente `ConfigCorreo.razor` en `Configuracion.razor` (solo Admin) + claves
  sembradas en `SeedData`; página `/comunicados` + enlace en `NavMenu` (Gestión).
- [x] **Reutilización de `PlantillasMensaje`** (antes solo copiaban texto): ahora también envían por
  correo y los recordatorios usan la plantilla de cobro. Destino por defecto: `Cliente.Email` /
  `Proveedor.Email` (editable por envío).
- [ ] Adjuntar el **PDF** del CFDI (hoy solo se adjunta el XML; no hay generador de PDF en el repo).
- [ ] Verificación manual pendiente (con BD levantada): SMTP real, prueba, auto-envío al timbrar,
  estados de cuenta y comunicado a correos reales.

---

## 3. 🟡 Auditoría y permisos granulares (roadmap #9 — parcial)

- [ ] **UI de matriz permisos × roles**: las tablas `Roles`/`Permisos`/`RolPermisos` ya existen en `SchemaPatch`, pero **no hay interfaz** ni consumo.
- [ ] **`IAutorizacionService.TienePermiso(recurso, accion)`** aplicado en `NavMenu.razor` (hoy solo ~15 filtros de rol duro) y en páginas críticas.
- [ ] **Auditoría de acciones de UI**: ampliar `AuditLog` (hoy: `PasswordChange`, `DosFa*`, `RfcValidated`, `FiscalSituationChanged`, `PasswordPolicyChanged`) a: crear/editar producto y precios, compra, ajuste de almacén, configuración, cierre de caja, transiciones de Cotizaciones/OV.
- [ ] **"Quién aprueba qué"** en transiciones de OV: hoy el motivo queda en `Notas`; falta registrar `usuario_id` del que confirma/suspende/reanuda/cancela (columna o `AuditLog`).
- [ ] **Pantalla de consulta de auditoría** (filtros por fecha/usuario/acción) — no hay UI.

---

## 4. 🟢 Comercial y crédito (diferenciadores del giro)

### 4.1 Límites de crédito

- [ ] Campos **`Cliente.LimiteCredito`** y **`Cliente.DiasCredito`** — verificado: **no existen** en `Cliente.cs`.
- [ ] Bloqueo automático en `VentasPOS` y facturación cuando el saldo vencido (1.2) supere el límite o haya mora.
- [ ] Aviso en alta/edición de cliente, búsqueda global y cotizaciones.

### 4.2 Listas de precios y descuento por volumen

- [ ] Entidades `ListaPrecio` + detalle (Público, Taller, Refaccionaria, Mayorista) — hoy solo `PrecioVenta`/`PrecioMayoreo`/`PrecioUnitario` en `Producto` (verificado: no existe `ListaPrecio`).
- [ ] Matriz de descuento por volumen (caja/tambor/escala) aplicada en POS, `CotizacionesService.Crear` y `CalcularTotales`.
- [ ] UI: catálogo de listas + asignación por cliente/tipo de cliente.

### 4.3 Guía de lubricación / compatibilidad

- [ ] Entidades `VehiculoModelo` y `AceiteCompatibilidad` (marca, modelo, año, motor, viscosidad recomendada, norma OEM, capacidad de cárter en litros) — verificado: no existen.
- [ ] Búsqueda "¿Qué aceite lleva un Versa 2020?" en la búsqueda de `VentasPOS.razor.cs` y en `CotizacionForm`.
- [ ] Seed del parque vehicular común en México.

### 4.4 Evaluación de proveedores

- [ ] Calificación de recepciones en `Compras` (cumplimiento, retrasos, bonos/nivel de servicio) + reporte simple.

---

## 5. 🟢 Logística de lubricantes

- [ ] **Tambor abierto / granel**: diferencia entre existencia sellada y fraccionada en uso (hoy `InventarioLote` solo descuenta litros) — evita vender un tambor cerrado cuando solo queda saldo abierto.
- [ ] **Rutas y reparto**: entidades `RutaEntrega`, `VehiculoReparto`, `Chofer` (verificado: no existen); hoja de ruta, remisión de reparto, liquidación entregado vs. devuelto.
- [ ] **Envases retornables**: control de tambores 208 L prestados al cliente y depósito/bonificación por devolución (cargo/abono en cuenta del cliente).
- [ ] **Carta Porte 3.x**: servicio + UI para CFDI de traslado (hoy solo hay XSLTs genéricos en `Infrastructure/Cfdi/xslt`, sin servicio ni página).

---

## 6. 🟡 Deuda técnica — funciones existentes a MODIFICAR

Verificado hoy en código (auditoría SOLID de Cotizaciones 12/10 + revisión hardcode).

### 6.1 Hardcode remanente en UI

- [ ] `Usuarios.razor:89-92` → iterar `Roles.Todos` (la constante ya existe en `Domain/Services/Roles.cs`; hoy los `<option>` siguen literales).
- [ ] `Compras.razor:250-254` → consumir `ICatalogosSatService` (formas de pago hardcodeadas, bypass del servicio).
- [ ] `Facturacion.razor:150-153` y `NotasCredito.razor:112-115` → motivos de cancelación centralizados en `CfdiDefaults` o catálogo (hoy duplicados a mano en ambas páginas).
- [ ] `Home.razor:292` ("IVA 16% incluido"), `Home.razor:309` ("Módulo futuro (PAC)"), `Home.razor:120` ("Módulo futuro") → calcular con `Impuestos.TasaIva` y mostrar estado real del PAC.
- [ ] Decisión: mover catálogos SAT de `CatalogosSatService` (diccionarios en código) a tabla `catalogos_sat` — impacta en OCP al agregar claves.

### 6.2 SOLID / DRY / mantenibilidad

- [ ] **B8 — helper único de sucursal**: el patrón `ctx.Rol == Roles.Admin ? sucursalId : ctx.SucursalId` está duplicado en ~15 sitios de 12 servicios (`FacturacionService.cs:21`, `ComprasService.cs:16`, `CorteCajaService.cs:19,30`, `ComplementoPagoService.cs:20`, …) → `ResolverSucursalAutorizada(...)` en `SucursalContext`/servicio de autorización.
- [ ] **DIP**: `Metas.razor.cs` y `VentasPOS.razor.cs` **aún inyectan `ErpDbContext` directo** (únicos dos verificados; el resto ya usa servicios) → mover a `IComisionesService`/`IVentasService`.
- [ ] **ISP (principal deuda de Cotizaciones)**: dividir `ICotizacionesService` (~20 métodos) en `ICotizacionesConsulta` + `ICotizacionesOperaciones`.
- [ ] **ISP (ventas)**: separar `IVentasService` (mezcla POS + despacho de almacén + devoluciones + auditoría) en `IVentasPosService` / `IEntregaAlmacenService` / `IHistorialVentasService`.
- [ ] **`IAsientoGeneradorService`**: `GenerarAsientosDeCompraAsync` y `GenerarAsientosDeDevolucionAsync` ignoran `sucursalId`/`usuarioId` → usarlos en el asiento o eliminar los parámetros.
- [ ] **REGLA #1 en lecturas**: `ObtenerCotizacionAsync` y `ObtenerOrdenVentaAsync` no filtran por sucursal → añadir guard (las 4 transiciones ya lo tienen).
- [ ] **Atomicidad**: envolver `ConvertirOrdenAVentaAsync` en transacción (`BeginTransactionAsync`) — hoy se crean factura/detalle/stock/kardex sin envolvente.
- [ ] **Consistencia de estados**: homogeneizar `Estado` string vs. enum en DTOs de resultados (heterogéneo entre servicios).
- [ ] **DRY de cálculo**: `CalcularTotales` se invoca ×3 en la creación de cotización → un único punto de cálculo.
- [ ] **A9 estados**: literales aislados restantes (p. ej. `ComplementoPago.cs:46`) → enum `EstadoFactura`.

### 6.3 CFDI / PAC — modo real

- [ ] Colocar `certs/csd.cer` + `certs/csd.key` (nunca al repo; la carpeta no existe → sigue en SIMULADO) y `dotnet user-secrets` (`Cfdi:KeyPassword`, `Cfdi__Pac__Usuario`, `Cfdi__Pac__Password`), `Cfdi:PacModo=TEST`.
- [ ] Prueba Finkok TEST: timbrar factura PUE, PPD + REP, cancelar con motivo 02 y consultar `EstadoSatAsync`.


---

## 7. 🔵 Verificación manual pendiente (con BD levantada)

Tomada de `Situación al 11 de octubre.md` §4 y validaciones CFDI:

- [ ] Login 2FA con app autenticadora + cambio de contraseña por vigencia vencida.
- [ ] Alta/edición cliente/proveedor: RFC inválido, RFC duplicado, situación Cancelada/Baja (debe bloquear).
- [ ] Escaneo de barras en POS (USB como teclado) + código inexistente (debe avisar en campana).
- [ ] Cierre y reapertura de comisión en `Metas` y portal del vendedor.
- [ ] Conciliación bancaria en UI (la prueba de integración dio 15/15; falta recorrido manual).
- [ ] Cotizaciones/OV en UI real (expiración al abrir listado, Suspender/Reanudar con motivo) — la prueba dio 5/5, falta smoke manual.
- [ ] Timbrado real Finkok (ver 6.3).
- [ ] Reportes financieros §1.4 con BD: cuadre de Balanza (Σdébitos = Σcréditos), Balance cuadrado (Activo = Pasivo + Patrimonio), Estado de Resultados vs. utilidad esperada y cambio de existencias contra kardex real de una sucursal.

## 8. 🟡 Documentación

- [ ] `README.md` (v0.0.3) no menciona módulos ya implementados: contabilidad, comisiones, cotizaciones/OV, bancos/conciliación, RFC+2FA, búsqueda global, plantillas → actualizar.
- [ ] `Situación al 11 de octubre.md` y este checklist quedan untracked hasta decidir el commit (working tree actual: A+B+C de Cotizaciones + fix FK `OrdenVenta.CotizacionId` + guards de sucursal + doc, **sin commitear**).

## 9. ✅ Recién completado — NO re-trabajar

- [x] **Gastos operativos §1.1 (13/10/2026 rev.2)**: verificación completa contra el código confirma que todos los ítems ya estaban implementados. `Gasto` entity + `GastoConfig` (EF, snake_case) + `GastosPatch` (CREATE TABLE, FK, CHECK categorías/importe) + `IGastoService`/`GastoService` (Crear/Editar/Eliminar/Listar/TotalesPorCategoria, REGLA #1) + póliza automática (`GastoService.Poliza.cs`, evento "Gasto" en `ReglasAsiento`, fallback 6000↔1000) + vinculación `CorteCajaService.Cierre.cs` (`GastosEfectivoTurnoAsync`) + página `/gastos` (Blazor, guard de rol, listado+formulario+reporte) + `NavLink` en Gestión + endpoint `/api/export/gastos/{sucursalId}` Excel. `IGastoService` registrado en `Program.cs`. Build `OilsMexico.slnx` 0 errores.
- [x] **Exportación Excel/PDF §2.1 (13/10/2026)**: `IExportacionService` + `ExportacionService` (9 Excel ClosedXML + 4 PDF QuestPDF), endpoints `/api/export/*` (Minimal API con guard de token) y botones de descarga en `/reportes`. Licencia QuestPDF Community en `Program.cs`. Build 0 errores. **Pendiente solo refinamiento**: verificación con BD (7), adjuntar PDF del CFDI al correo y botones en las demás páginas.
- [x] **Reportes financieros §1.4 (servicio + UI, 13/10/2026)**: `IReportesFinancierosService` + `ReportesFinancierosService` (parcial: balanza, balance, estado de resultados, cambio de existencias, indicadores) y **página `/reportes` con 5 pestañas que consumen el servicio** (carga perezosa `??=`), registrado en `Program.cs`. Build `OilsMexico.slnx` 0 errores. Corrige el esquema real del dominio (kardex `Tipo` string, `InventarioSucursal.StockActual`, `Producto` sin `SucursalId`); `IndicadoresAsync(null)` muestra todas las sucursales para Admin. `NavLink` en Gestión. **Pendiente solo refinamiento**: exportación Excel/PDF (2.1 ✅ hecho), costo FIFO real (1.3) y verificación con BD (7).
- [x] Cotizaciones Capa A+B+C: pipeline cotización → OV → factura, expiración al cargar listado (`ExpirarCotizacionesAsync`), Suspender/Reanudar con guards estrictos y motivo en `Notas`.
- [x] Fix FK: `OrdenVenta.CotizacionId` `int` → `int?` (manual = null) + `CotizacionesPatch` idempotente.
- [x] REGLA #1 (guard de sucursal) en las 4 transiciones: `ConfirmarOrdenVentaAsync`, `SuspenderOrdenVentaAsync`, `ReanudarOrdenVentaAsync` (`Ordenes.cs`) y `CancelarOrdenVentaAsync` (`Convertir.cs`).
- [x] Conciliación bancaria (`Bancos.razor`, 15/15) · RFC + situación fiscal (`RfcValidador`) · contraseña + 2FA · barras/escaneo · wizard 3 pasos · búsqueda global Ctrl+K · plantillas de mensaje · comisiones ampliadas.
- [x] Hardcode: `Roles.Admin`, `CfdiDefaults`, `Impuestos`, `ReglasAsiento`, `ImportacionReglas`, umbrales ABC en configuración.
- [x] Prueba de integración Capa C 5/5 (C1, C2, C2b, C2c) y build 0 errores.

## 10. ⚪ Fuera de alcance (decisión de diseño)

No considerados bloqueantes para "ERP completo" (análisis 09/10): nómina/RRHH · producción/BOM/MRP · multiempresa · microservicios · API REST para terceros.

---

### Orden sugerido de ataque

**1 → 2 → 3** (mismo orden del roadmap v11; lo cerrado no se re-trabaja), atacando la **sección 6 en paralelo** por ser esfuerzo bajo y alto impacto (6.1 → 6.2 B8/DIP → 6.3). Las secciones 4 y 5 son diferenciadores del giro: opcionales para "completo", valiosas para el negocio.

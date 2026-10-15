# Checklist de completitud del ERP — 13 de octubre de 2026

> Lista maestra de **cada función que falta o necesita modificación/mejora** para que OilsMexico ERP quede completo.
> Cada ítem fue **verificado contra el código** (build `OilsMexico.slnx` 0 errores; Cotizaciones Capa A+B+C probadas 5/5 en PostgreSQL desechable). Última actualización **13/10/2026** (rev. 4): §1.3 implementado y compilado (motor FIFO + cierre de período + bloqueo de asientos + UI), probado 20/20 con EF InMemory (10 nuevas en `CierreContableTests` + 10 de regresión); pendiente smoke manual con PostgreSQL productiva y pruebas SMTP/crédito (§4.1).
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

### 1.3 Costo de ventas FIFO + cierre de período — roadmap #4 ✅ IMPLEMENTADO (13/10/2026 rev.4)

**Servicio + UI implementados** (build `OilsMexico.slnx` 0 errores; 10/10 pruebas nuevas en `tests/OilsMexico.IntegrationTests/CierreContableTests.cs` con EF InMemory + 10/10 de regresión; parche SQL idempotente `PeriodoContablePatch`).

- [x] **Motor FIFO** (`AsientoGeneradorService.CostoVenta.cs`, `CalcularCostoVentasFifoAsync`): reconstruye las capas de inventario replantando el kardex de la sucursal en orden cronológico — entradas `COMPRA` toman el costo por litro del renglón de compra (`CompraDetalle.CostoUnitario` / `FactorConversion`; respaldos: costo de la capa vigente y `Producto.PrecioCosto`) y las salidas consumen capas FIFO. El costo de las salidas `VENTA` del periodo se atribuye a la factura que las originó **solo si sigue activa** (misma regla de ingreso que el Estado de Resultados: no cancelada ni devuelta), para que costos e ingresos cuadren. *Nota: a diferencia de lo que decía el checklist, `InventarioLote` **no** almacena costo — el costo se reconstruye del kardex + compras.*
- [x] **Asiento de costo de venta** (`GenerarAsientosDeCostoVentaAsync`): líneas del evento `"CostoVenta"` en `ReglasAsiento` si está sembrado, o fallback **5000 ↔ 1200** (Costo de Ventas vs. Almacén). Vacío si no hay costo en el periodo.
- [x] **Cierre contable mensual** (`ICierreContableService` + `CierreContableService`): `CerrarAsync` (Admin/Conta) siembra el catálogo de cuentas faltante, registra la póliza fechada al **último instante local del mes** (concepto marcador `Cierre aaaa-mm · Costo de ventas FIFO`, idempotente: un cierre a medias reutiliza la póliza) y marca el periodo en `periodos_contables` (índice único sucursal+año+mes = guard de concurrencia). `RegistrarAsientoAsync` **rechaza escrituras fechadas en periodos cerrados** (fail-closed, incluso para Admin; `RegistrarAsientoRequest` ahora acepta `FechaUtc` opcional). `ReabrirAsync` **solo Admin**: elimina la póliza de cierre, revierte sus saldos (`SaldoActual`) y la bandera.
- [x] **Estado de Resultados del periodo alimentado por esos asientos** (`ReportesFinancierosService.CostoVentasDelRangoAsync`): los meses COMPLETOS cerrados leen el importe de su póliza de cierre (fuente contable); el resto del rango se calcula en vivo con el mismo motor FIFO → mismo número que el libro. `IndicadoresAsync` también usa FIFO (ya no usa `Producto.PrecioCosto`).
- [x] **UI**: pestaña **"Cierre contable"** en `/reportes` (lista de periodos cerrados con costo y póliza; Cerrar para Admin/Conta; Reaprar visible solo para Admin; avisos de resultado). Guard de rol Admin/Conta de la página incluido.
- [x] **Catálogo de cuentas sembrado** (`CuentasContablesService.AsegurarCatalogoAsync`, idempotente, invocado desde `SeedData` en cada arranque y desde el cierre): antes **nada** siembra `cuentas_contables` (los reportes salían vacíos); ahora se crean por sucursal los códigos 1000…6000 **+ 5000 "Costo de ventas"** (solo faltantes).
- [ ] **Verificación con BD PostgreSQL productiva**: los tests usan EF InMemory; falta smoke manual de `PeriodoContablePatch` + cierre/reabrire en la BD real (ver 7).

**Pendiente (refinamiento / decisión de diseño):**
- [ ] POS y Notas de Crédito **no validan periodo cerrado** (solo se bloquean asientos): si se vende en un mes ya cerrado, la póliza de cierre conserva el costo previo. Decidir si bloquear ventas en meses cerrados o recomputar al reabrir.

### 1.4 Reportes financieros — roadmap #7

**Servicio + UI implementados al 13/10/2026** (`IReportesFinancierosService` + `ReportesFinancierosService` parcial `.cs`/`.Parte2.cs` + página `/reportes` con tabs, build 0 errores, registrado en `Program.cs`). Fuente: `CuentasContables` / `AsientoContable` / `DetalleAsiento` / `Facturas` / kardex (`Movimientos`) existentes. Todo respeta REGLA #1 vía `ResolverSucursalAutorizada`.

- [x] **Balanza de Comprobación** por periodo y sucursal (débitos/créditos + saldo deudor/acreedor por cuenta).
- [x] **Balance General** al corte (Activo = Pasivo + Patrimonio, con corte UTC correcto + indicador "cuadrado/descuadrado").
- [x] **Estado de Resultados por sucursal y periodo** (ventas netas, costo de ventas por `Producto.PrecioCosto`, utilidad bruta, IVA trasladado, márgenes).
- [x] **Informe de cambio de existencias** (inicial derivado + compras − ventas ± ajustes ± traspasos/devoluciones, por producto).
- [x] **Indicadores por sucursal y periodo** (margen, rotación de inventario, días de inventario, ticket promedio, nº ventas, inventario promedio). Con "Todas" (null) un Admin ve todas las sucursales activas; el resto la suya (REGLA #1).
- [x] **Página `/reportes` consumiendo el servicio** (carga perezosa por pestaña, `??=` para no recalcular): 5 pestañas (Balanza/Balance/Resultados/Existencias/Indicadores) con tablas Bootstrap + `NavLink` "Reportes Financieros" en menú Gestión; filtros sucursal/desde/hasta/corte y guard de rol `Admin`/`Conta`.

**Pendiente (refinamiento):**
- [x] **Costo de ventas FIFO real** en Estado de Resultados — **hecho en §1.3 (rev.4)**: motor FIFO (meses abiertos) + lectura de la póliza de cierre (meses cerrados); también aplicado a Indicadores.
- [ ] **Exportación Excel/PDF** de los 5 reportes (`IExportacionService.FinancieroPdfAsync` ya está declarada en la interfaz, falta implementación) — ver 2.1.
- [ ] **Verificación con BD** de los 5 reportes (ver 7).

### 1.4b (nuevo) Estado de Resultados vs. asientos contables

- [ ] Unificar la fuente: el Estado de Resultados del servicio calcula desde `Facturas`/`Producto`, mientras el Balance usa `AsientoContable`. **Avance rev.4 (§1.3)**: el costo de ventas ya cuadra con el libro (meses cerrados leen su póliza de cierre; abiertos usan el mismo motor FIFO). Siguen sin unificarse ingresos y gastos (que dependen de que Ventas/Gastos registren póliza automática por transacción).

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

- [x] **UI de matriz permisos × roles**: `/permisos`, exclusiva de Admin; cambios persistidos en `RolPermisos`, con permisos de Admin fijos y seed inicial compatible con relaciones previas.
- [x] **Autorización por página**: `IAutorizacionService` aplicada al menú y a páginas ERP críticas; bloquea acceso directo y comprueba el permiso antes de cargar datos. La matriz define el acceso inicial equivalente a los roles actuales.
- [ ] **Auditoría de acciones de UI**: ampliar `AuditLog` (hoy: `PasswordChange`, `DosFa*`, `RfcValidated`, `FiscalSituationChanged`, `PasswordPolicyChanged`) a: crear/editar producto y precios, compra, ajuste de almacén, configuración y cierre de caja. **Implementado:** transiciones de OV (confirmar, suspender, reanudar, cancelar y facturar) registran actor, motivo y estado anterior/nuevo.
- [x] **"Quién aprueba qué"** en transiciones de OV: `AuditLog` registra `usuario_id`, motivo y estados anterior/nuevo para confirmación, suspensión, reanudación, cancelación y facturación.
- [x] **Pantalla de consulta de auditoría** (`/auditoria`, solo Admin; filtros por fecha, usuario y acción, paginación).

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

- Automatización PostgreSQL P1: suite completa de integración **35/35** y pruebas PostgreSQL focalizadas **7/7**. La cobertura incluye CxC/CxP, parches, cierre/reapertura, Balanza, Estado de Resultados, existencias, crédito y conversión OV → venta. Las verificaciones manuales y los escenarios señalados abajo siguen pendientes.
- [ ] Login 2FA con app autenticadora + cambio de contraseña por vigencia vencida.
- [ ] Alta/edición cliente/proveedor: RFC inválido, RFC duplicado, situación Cancelada/Baja (debe bloquear).
- [ ] Escaneo de barras en POS (USB como teclado) + código inexistente (debe avisar en campana).
- [ ] Cierre y reapertura de comisión en `Metas` y portal del vendedor.
- [ ] Conciliación bancaria en UI (la prueba de integración dio 15/15; falta recorrido manual).
- [ ] Cotizaciones/OV en UI real (expiración al abrir listado, Suspender/Reanudar con motivo) — la prueba dio 5/5, falta smoke manual.
- [ ] Timbrado real Finkok (ver 6.3).
- [ ] Reportes financieros §1.4 con BD: las pruebas automatizadas PostgreSQL cubren Balanza, Estado de Resultados y existencias; falta validar manualmente Balance (Activo = Pasivo + Patrimonio), utilidad esperada y cambio de existencias contra el kardex real de una sucursal. Los ingresos de facturas históricas sin póliza no se retrocontabilizan; el reporte del libro no los incluye.
- [ ] Cierre de periodo §1.3 con BD real (PostgreSQL): las pruebas automatizadas cubren parche, cierre/reapertura y bloqueo; falta el recorrido manual de cerrar un mes con ventas (póliza 5000↔1200) y confirmar reapertura Admin.

## 8. 🟡 Documentación

- [ ] `README.md` (v0.0.3) no menciona módulos ya implementados: contabilidad, comisiones, cotizaciones/OV, bancos/conciliación, RFC+2FA, búsqueda global, plantillas → actualizar.
- [ ] `Situación al 11 de octubre.md` y este checklist quedan untracked hasta decidir el commit (working tree actual: A+B+C de Cotizaciones + fix FK `OrdenVenta.CotizacionId` + guards de sucursal + doc, **sin commitear**).

## 9. ✅ Recién completado — NO re-trabajar

- [x] **Costo de ventas FIFO + cierre de período §1.3 (13/10/2026 rev.4)**: motor FIFO en `AsientoGeneradorService.CostoVenta.cs` (capas reconstruidas del kardex; costo por litro de `CompraDetalle.CostoUnitario`/`FactorConversion`; respaldo `Producto.PrecioCosto`; facturas canceladas/devueltas fuera, misma regla que ingresos) + asiento Costo de Ventas vs. Almacén (evento `"CostoVenta"` / fallback **5000↔1200**) + `ICierreContableService`/`CierreContableService` (`CerrarAsync` Admin/Conta idempotente con póliza fechada al cierre del mes + bandera `periodos_contables` vía `PeriodoContablePatch` e índice único sucursal+año+mes; `ReabrirAsync` **solo Admin** revierte saldos y póliza) + **bloqueo de escritura en periodos cerrados** en `CuentasContablesService.RegistrarAsientoAsync` (`RegistrarAsientoRequest.FechaUtc` opcional) + Estado de Resultados e Indicadores con costo FIFO (meses cerrados leen su póliza, §1.4) + pestaña **"Cierre contable"** en `/reportes` + siembra idempotente del catálogo de cuentas por sucursal en `SeedData` (**incluye la cuenta 5000; antes no se sembraba ninguna cuenta**). Build `OilsMexico.slnx` 0 errores; **10/10 pruebas nuevas** (`CierreContableTests`: FIFO 1600 = 100×10+50×12, póliza/bloqueo, reapertura Admin con reversión, ER cuadra con póliza, REGLA #1, sin ventas, futuro, cancelada, doble cierre) + 10/10 regresión de Exportación.

- [x] **Gastos operativos §1.1 (13/10/2026 rev.2)**: verificación completa contra el código confirma que todos los ítems ya estaban implementados. `Gasto` entity + `GastoConfig` (EF, snake_case) + `GastosPatch` (CREATE TABLE, FK, CHECK categorías/importe) + `IGastoService`/`GastoService` (Crear/Editar/Eliminar/Listar/TotalesPorCategoria, REGLA #1) + póliza automática (`GastoService.Poliza.cs`, evento "Gasto" en `ReglasAsiento`, fallback 6000↔1000) + vinculación `CorteCajaService.Cierre.cs` (`GastosEfectivoTurnoAsync`) + página `/gastos` (Blazor, guard de rol, listado+formulario+reporte) + `NavLink` en Gestión + endpoint `/api/export/gastos/{sucursalId}` Excel. `IGastoService` registrado en `Program.cs`. Build `OilsMexico.slnx` 0 errores.
- [x] **Exportación Excel/PDF §2.1 (13/10/2026)**: `IExportacionService` + `ExportacionService` (9 Excel ClosedXML + 4 PDF QuestPDF), endpoints `/api/export/*` (Minimal API con guard de token) y botones de descarga en `/reportes`. Licencia QuestPDF Community en `Program.cs`. Build 0 errores. **Pendiente solo refinamiento**: verificación con BD (7), adjuntar PDF del CFDI al correo y botones en las demás páginas.
- [x] **Reportes financieros §1.4 y contabilización P1**: `IReportesFinancierosService` + `ReportesFinancierosService` generan ingresos, IVA y gastos desde el libro; las ventas nuevas generan póliza y las notas de crédito revierten la póliza original. La conversión OV → venta guarda factura, inventario, póliza, vínculo y auditoría en una sola transacción. **Pruebas PostgreSQL P1** cubren CxC/CxP, parches, cierre/reapertura, Balanza, Estado de Resultados, existencias, crédito y atomicidad OV → venta; suite completa 35/35 y focalizada 7/7 aprobadas. **Alcance histórico acordado**: no retrocontabilizar facturas antiguas sin póliza para preservar periodos cerrados; esos ingresos no aparecen en el Estado de Resultados basado en el libro. Pendiente recorrido manual de §7 y validación externa SMTP/PAC.
- [x] Cotizaciones Capa A+B+C: pipeline cotización → OV → factura, expiración al cargar listado (`ExpirarCotizacionesAsync`), Suspender/Reanudar con guards estrictos y motivo en `Notas`.
- [x] Fix FK: `OrdenVenta.CotizacionId` `int` → `int?` (manual = null) + `CotizacionesPatch` idempotente.
- [x] REGLA #1 (guard de sucursal) en las 4 transiciones: `ConfirmarOrdenVentaAsync`, `SuspenderOrdenVentaAsync`, `ReanudarOrdenVentaAsync` (`Ordenes.cs`) y `CancelarOrdenVentaAsync` (`Convertir.cs`).
- [x] Conciliación bancaria (`Bancos.razor`, 15/15) · RFC + situación fiscal (`RfcValidador`) · contraseña + 2FA · barras/escaneo · wizard 3 pasos · búsqueda global Ctrl+K · plantillas de mensaje · comisiones ampliadas.
- [x] Hardcode: `Roles.Admin`, `CfdiDefaults`, `Impuestos`, `ReglasAsiento`, `ImportacionReglas`, umbrales ABC en configuración.
- [x] Prueba de integración Capa C 5/5 (C1, C2, C2b, C2c) y build 0 errores.
- [x] Auditoría de transiciones OV + consulta `/auditoria` (§3): actor, motivo y estados en `AuditLog`; pantalla Admin filtrable por fecha/usuario/acción. 3 pruebas de integración cubren filtros, rechazo para rol no Admin y actor/estados en transiciones.

## 10. ⚪ Fuera de alcance (decisión de diseño)

No considerados bloqueantes para "ERP completo" (análisis 09/10): nómina/RRHH · producción/BOM/MRP · multiempresa · microservicios · API REST para terceros.

---

### Orden sugerido de ataque

**1 → 2 → 3** (mismo orden del roadmap v11; lo cerrado no se re-trabaja), atacando la **sección 6 en paralelo** por ser esfuerzo bajo y alto impacto (6.1 → 6.2 B8/DIP → 6.3). Las secciones 4 y 5 son diferenciadores del giro: opcionales para "completo", valiosas para el negocio.

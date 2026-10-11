He revisado el código completo de la solución y su documentación. Aquí está el análisis.


## Resumen ejecutivo

**OilsMexico ERP** ya cubre un núcleo muy completo de un ERP para distribuidora de aceites/lubricantes en México (`.NET 10` + PostgreSQL + EF Core + Blazor Server + SignalR). Lo que no existe es un **mundo bien definido de "lo que falta"**, así que hice la matriz comparando lo implementado (codes basado, entidades, API/UI, documentos) con el alcance de un ERP "completo" para ese negocio.

> **Alcance de este análisis:** funcionalidades del dominio del negocio (venta, almacén, compras, facturación electrónica, contabilidad, comisiones, reportes y operaciones). Para no extrapolar: no incluye módulos de RRHH/nómina, producción/BOM/MRP, multiteratidad ni microservicios, que son decisión de diseño de la empresa y no del caso.


## 1. Lo que ya existe y está operativo

| Área | Módulos ya implementados |
| - | - |
| **Seguridad** | Login PIN + sesión persistente en `ProtectedSessionStorage`, hash SHA-256, bloqueo por intentos, timeout por inactividad, token de sesión revocable |
| **Multi-sucursal** | Aislamiento por `SucursalId` de sesión; Admin puede ver cualquier sucursal; `sucursales` con datos fiscales del emisor |
| **Ventas** | POS (cesta, búsqueda por SKU/marca/viscosidad, combinación de IVA 16%, FormaPago/Método/UsoCfdi, toggle CFDI), SignalR por sucursal |
| **Historial** | Filtros por fecha/estado/folio-cliente, resumen, paginación, detalle extendible, **Surtir / Devolver** con reposición de stock y rastreo en kardex |
| **Almacén** | Entradas/compras, ajustes, traspasos, kardex auditado, lotes con caducidad, descuento FIFO |
| **Factoría** | Facturación CFDI 4.0: sellado nativo RSA-SHA256 (CSD `.cer/.key`), timbrado vía PAC `TEST/PRODUCCION/SIMULADO`, cancelación real `cancel\_signature` ante Finkok, descarga XML, NC electrónica (Tipo E + relación 01), REP/cobro parcial (Tipo P + Pagos 2.0) |
| **Compras** | Ciclo orden → recepción parcial/total (suma stock + lote + kardex COMPRA) → CxP con forma SAT (Pagada/Parcial/Pendiente) + **CuentasPorPagar** |
| **Clientes/Proveedores** | CRUD, datos fiscales RFC, dirección con autocompletado SEPOMEX (CAT oficial + Excel), combos obligatorios de colonia |
| **Gestión de compras** | Dashboard, **Cuentas por cobrar** (CPA con saldo/estados), contabilidad básica (cuentas, asientos, libro mayor, saldos) |
| **Comisiones** | Cuota mensual por vendedor, portal del vendedor, cálculo en cash basis, cierre congelado, histórico, reabrir período |
| **Contabilidad** | Cuentas contables básicas (portador), asientos de venta/compra/devolución, libro mayor por cuenta, saldos por cuenta |
| **Reportes** | Dashboard de gestión, ventas por día/producto/marca, rotación ABC, cortes por sucursal, inventario por viscosidad |
| **Caja** | Corte de caja (apertura, fotografía del sistema, cuadre de contado, cierre, historial) |
| **Utilidades** | SEPOMEX (TXT/CSV/Excel oficial, estadísticas), ticket 58mm, Datos de la sucursal (rol Admin/lectura), Configuración de timeout |


Estructura esperada en el código verificado: `Domain` (~30 entidades + enums), `Application` (interfaces/DTOs), `Infrastructure` (ErpDbContext, ~20 servicios, Persistence/Configs), `Web` (Program.cs, `ErpHub` SignalR, ~30 páginas Razor + `.razor.cs`, servicios de sesión y temporizador). No hay métodos lanzando `NotImplementedException` pendientes del desarrollador; el código está coherente.


## 2. Faltan funcionalidades clave (por módulo)

### 🔴 Alta prioridad — bloqueantes para un "ERP completo"

1. **Cotización y órdenes de venta (SO/CT)**

   - Sin ellas no hay control de compromiso de precio, no se puede convertir cotización en venta, y no hay seguimiento de pre-ventas.

   - Lo que falte: `Cotizacion` (con validez, líneas, estado: Borrador/Expirada/Convertida), `OrdenVenta`, conversión automática con **confirmación de precio** y asignación de vendedor.

   - Estado: **no existe** en el menú, en la entidad ni en la API.

2. **Registro de gastos operativos y de automotor**

   - Una distribuidora necesita registrar gastos: gasolina, lubricantes de taller, herramientas, reparación de vehículos, imprenta, servicios públicos, entretenimiento, etc.

   - Consecuencia: el saldo de caja no concilia con el negocio real.

   - Necesario: entidad `Gasto`, página `/gastos`, forma de pago, clasificación por cuenta contable, y flujo de caja asociado.

3. **Cuentas por cobrar / cuentas por pagar con vencimientos**

   - CPA (clientes): ya existe el servicio `EstadoCuentasService` y la página `estado-cuentas`, **pero** no hay vencimientos de plazos, estado de la factura CFDI, ni formato de conocimiento.

   - CPP (proveedores): existe `ComprasService.CuentasPorPagarAsync` en la API, pero **no hay página/nav** ni detalle de vencimiento.

   - Faltan: plazos de pago, mora, punto de corte en CFDI de crédito, reportes de insolvencia.

4. **Contabilidad de costo de ventas**

   - El sistema registró `PrecioCosto` en `Producto` y está el `IAsientoGeneradorService`, pero **no hay servicio ni UI que cargue los costos por lote en los asientos de cierre** (precio de costo aplicado según lote/flujo FIFO).

   - Sin esto, el estado de resultados no refleja el costo real de las ventas.

5. **Cierre de periodo contable**

   - No hay asientos de cierre automáticos: valor de inventarios en mano, depreciación, acumulación de costos, transferencias de caja.

   - Mínimo: botón "Cierre de período" + auditoría de pólizas abiertas.

6. **Validación de RFC y situación fiscal del receptor**

   - `Cliente` y `Proveedor` aceptan RFC sin validación (solo valor por defecto) y no tienen `TipoPersona` (física/jurídica) ni situación fiscal.

   - El SAT exige RFC válido y correcto para emitir CFDI. Es crítico para los comprobantes.


### 🟡 Media prioridad — control y cumplimiento

1. **Producto receptor y facturación por correo / enlace**

   - No hay `CFDI` de débito ni comprobantes de enlace (A, P2P, P49...), ni opción de "facturar por correo" con registro de ENVÍO.

2. **Conciliación bancaria**

   - No hay registro de transferencias/cheques con años de operación, ni reconciliación de estados de cuenta bancaria.

   - `CompraPago` y `VentaCobro` registran el pago, pero no la operación bancaria.

3. **Evaluación de proveedores**

   - No existen calificaciones, retrasos, bonos o nivel de servicio (LTV).

4. **Reportes finales de contabilidad y cotización**

   - No hay **balance**, **estado de resultados**, **informe de cambio de existencias**, **indicadores de gestión** por sucursal y período.

   - Los reportes de gestión actuales son de ventas/rotación; faltan finanzas y operaciones.

5. **Exportación de reportes y listados**

   - Solo hay `ExcelDataReader` (lectura). No hay librería de exportación (CSV/Excel/PDF). Los reportes se ven en pantalla y no se pueden imprimir/exportar.

   - Falta: exportar facturas, CPA/CPP, inventario, rotationes, comprobantes, listas de entrega.

6. **Auditoría y permisos más granulares**

   - El kardex cubre inventario. No hay auditoría de acciones de la UI (usuarios, configuración, productor, compras, contratos).

   - Solo 4 roles estáticos; no hay permisos por menú/recurso ni registro de quién aprueba qué.

### 🟢 Baja prioridad / oportunidades

1. **Cambio de contraseña, recuperación de cuenta y 2FA**

   - Solo bloqueo y timeout existen; no hay cambio de contraseña, reclamo de cuenta ni autenticación de dos factores.

2. **Codificación de barras y escaneo**

   - Hay `CodigoBarra` en `UnidadMedida`, pero no hay página/validación de escaneo de código de barras durante la búsqueda del POS.

3. **Ayuda y ojkeko contextual**

   - Sin wizard para alta de cliente, sin help, sin notificaciones internas.

4. **Codificación de barras (productos)**

   - Técnicamente se tiene `CodigoBarra` en `UnidadMedida`, pero no existe UI/escaneo. (Menor)

5. **Exportación de datos**

   - Como punto 11.


## 3. Propuesta de roadmap (priorización)

| Orden | Prioridad | Funcionalidad | Justificación |
| - | - | - | - |
| 1 | 🔴 | Cotizaciones/OR para ventas (SO/CT) | Control de precio, pre-ventas, proceso de venta correcto |
| 2 | 🔴 | Registro de gastos operativos | Reconciliación de caja con la operación real |
| 3 | 🔴 | CPA y CPP con vencimientos | Control de riesgo crediticio/y financiero |
| 4 | 🔴 | Costo de ventas y cierre de período | Estado de resultados y estados financieros |
| 5 | 🔴 | Validación RFC/situación fiscal del receptor | Cumplimiento SAT y emisión correcta |
| 6 | 🟡 | Conciliación bancaria | Control de efectivo y proveedores |
| 7 | 🟡 | Reportes de contabilidad/cotización | Estado de resultados por sucursal |
| 8 | 🟡 | Exportación a Excel/PDF | Reportes operativos y entrega a clientes |
| 9 | 🟡 | Auditoría de la UI y permisos | Seguridad interna y trazabilidad |
| 10 | 🟢 | Cambio contraseña, 2FA | Mejores prácticas de seguridad |
| 11 | 🟢 | Ayuda, búsqueda global, plantillas | Usabilidad y productividad |



## 4. Limitaciones y suposiciones del análisis

- **Suposición clave:** el objetivo es completar un ERP para **distribuidora de aceites y lubricantes automotrices en México** (multi-sucursal, POS, CFDI 4.0, comisiones), no un SAP corporativo genérico.

- No evalué si hay módulos "en cola" que aún no están documentados; lo que presento es un delta del código y la documentación actual.

- Algunos puntos (nómina, producción, multi-empresa, API REST para terceros) se pueden agregar más adelante; no se consideran bloqueantes del negocio actual.

- Los archivos `docx` del repositorio no los pude leer como texto (binarios), así que usé la documentación `md` y el código como fuente de verdad.


Si quieres, puedo ayudarte a:

1. Elegir **por ahora cuáles implementar** según costo/beneficio (te dejo el archivo de "roadmap" ya listo),

2. Diseñar el **modelo de datos y la API** de la prioridad \#1 (cotizaciones/OR),

3. Revisar el código para **completar alguna funcionalidad en particular**.


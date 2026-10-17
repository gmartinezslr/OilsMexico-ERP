Lo que falta para el 16 de octubre


revisa TODO el proyecto y dame un porcentaje total de lo que falta para terminar el ERP, porcentaje y tareas lo más detalladas posible. Evita lo que ya existe y está funcionando correctamente y no necesita ajustes, cambios o modificaciones





Ya tengo toda la información necesaria. Aquí está mi análisis completo del proyecto.


# ANÁLISIS DE COMPLETITUD — OilsMexico ERP

## 📊 RESUMEN EJECUTIVO

| Métrica | Valor |
| - | - |
| **ERP núcleo operativo** (ventas, almacén, compras, CFDI, contabilidad, caja, reportes) | ~90 % completado |
| **ERP "completo"** (incluye bloqueantes funcionales, deuda técnica, diferenciadores opcionales, verificación y documentación) | ~85 % completado |
| **Faltante total para terminar el ERP** | **~15 %** |
| Estado del build | `dotnet build OilsMexico.slnx` → 0 errores, 0 advertencias |
| Pruebas automatizadas | 35/35 integradas aprobadas; 7/7 PostgreSQL focalizadas aprobadas |



## ✅ ÁREAS YA COMPLETAS (NO cuentan como faltante)

Estas áreas ya están implementadas y funcionando correctamente. **No las re-trabajes:**

| Área | Estado | Evidencia |
| - | - | - |
| Autenticación, login, 2FA, política de contraseña | 100 % | `AuthService`, `PasswordHistory`, `DosFa`, `PasswordConfigDto` |
| Clientes y Proveedores (multi-correo, teléfonos, direcciones) | 100 % | `ContactoTercero`, `CorreoTercero`, `TelefonoTercero`, `DireccionTercero` |
| Productos, Almacén, Inventario, Kardex, Lotes, Unidades de medida | 100 % | `Producto`, `InventarioLote`, `InventarioSucursal`, `MovimientoInventario`, `UnidadMedida` |
| Códigos de barras y escaneo | 100 % | `CodigosBarra`, `Ticket.razor`, POS con escaneo |
| Cotizaciones → Órdenes de Venta → Factura (pipeline completo) | 100 % | `Cotizacion`, `OrdenVenta`, `Factura`, conversión transaccional |
| CFDI 4.0 timbrado PUE con Finkok TEST (CSD validado, UUID obtenido) | 100 % (PUE) | `PacTimbradoService`, certificado `CSD\_Sucursal\_1\_...`, `user-secrets` |
| Caja, Corte de caja, Gastos operativos | 100 % | `CorteCaja`, `Gasto`, `GastosEfectivoTurnoAsync`, página `/gastos` |
| Bancos y Conciliación bancaria | 100 % | `Bancos.razor`, `NaturalezaMovimiento`, `EstadoConciliacion` |
| CxC / CxP con vencimientos y antigüedad (0-15/16-30/31-60/+60 días) | 100 % | `EstadoCuentasService.Antiguedad`, `ComprasService.Antiguedad` |
| Contabilidad: asientos, pólizas, cierre de periodo, FIFO, ReglasAsiento | 100 % | `AsientoContable`, `DetalleAsiento`, `PeriodoContable`, `ReglaAsiento`, motor FIFO |
| Reportes financieros (Balanza, Balance, ER, Existencias, Indicadores) | 100 % (servicio + UI) | `IReportesFinancierosService`, página `/reportes` con 5 pestañas |
| Exportación Excel (9 reportes) + PDF (4 reportes, incl. CFDI con QR SAT) | 100 % (código) | `ExportacionService`, `ClosedXML`, `QuestPDF`, endpoints `/api/export/\*` |
| Correo SMTP con MailKit, envío de CFDI (XML adjunto), estados de cuenta, comunicados | 100 % (código) | `EmailService`, `EnviarCfdiAsync`, `ConfigCorreo.razor`, `/comunicados` |
| Auditoría: transiciones OV, pantalla `/auditoria`, `AuditLog` | 100 % (transiciones OV) | `Auditoria.razor`, 3 pruebas de integración |
| Matriz de permisos × roles (UI `/permisos`) | 100 % | `Permisos.razor`, `RolPermisos`, `PermisosSeed` |
| Búsqueda global Ctrl+K, plantillas de mensaje, wizard 3 pasos | 100 % | `BusquedaGlobal`, `PlantillasMensaje` |
| Comisiones (cuotas, metas, cierres, reaperturas) | 100 % | `CuotaVendedor`, `ComisionesService` (multi-archivo) |
| Configuración (SMTP, PAC, umbrales ABC, seed data) | 100 % | `ConfiguracionService`, `SeedData`, `configuracion` tabla |
| Migrations, parches de esquema idempotentes | 100 % | `ErpDbContext`, `SchemaPatch`, `SeedData`, múltiples `\*Patch.cs` |
| Hardcode §6.1 (roles, formas de pago, motivos cancelación, IVA, PAC) | 100 % (cerrado) | Verificado en `Deuda técnica al 15 de octubre.md` |



## 🔴 ÁREAS CON FALTANTE — EL 15 % RESTANTE

A continuación, la lista detallada de **todas las tareas que realmente faltan**, organizada por prioridad y área. Cada ítem incluye qué falta exactamente, archivos afectados y esfuerzo estimado.


### PRIORIDAD 0 — BLOQUEANTES FUNCIONALES (críticos para producción)

#### T-01. Bloqueo por morosidad en POS y Facturación (§6.1 / §4.1)

**Estado:** Los campos `Cliente.DiasCredito` y `Cliente.LimiteCredito` YA EXISTEN en `Cliente.cs` (líneas 25-26), pero **no hay lógica de bloqueo** que impida vender a un cliente con saldo vencido superior al límite o con mora activa.

**Qué falta exactamente:**

1. Servicio de validación de crédito: antes de crear Factura/POS, verificar `Cliente.LimiteCredito` vs. saldo vencido (CxC antigüedad)

2. Bloqueo en `VentasPOS.razor.cs` y `Facturacion.razor.cs`: si mora activa o límite excedido → rechazar venta con mensaje claro

3. Aviso visual en alta/edición de cliente (`Clientes.razor`), búsqueda global y cotizaciones cuando el cliente tiene límite de crédito

4. Pruebas: cliente con límite $0 y saldo vencido → no puede facturar; cliente con límite OK → puede facturar

**Archivos afectados:** `VentasPOS.razor.cs`, `Facturacion.razor.cs`, `Clientes.razor.cs`, `ClientesService.cs`, nuevo servicio `ICreditoService` / `CreditoService` **Esfuerzo estimado:** 1-2 días


#### T-02. Cierre de periodo contable — bloquear ventas y asientos en periodos cerrados (§6.1 / §1.3)

**Estado:** El bloqueo de asientos contables en periodos cerrados YA EXISTE en `CuentasContablesService.RegistrarAsientoAsync`. Sin embargo, **POS y Notas de Crédito no validan el periodo cerrado** — solo se bloquean los asientos, no la venta.

**Qué falta exactamente:**

1. Decisión de negocio: ¿impedir ventas/notas de crédito en mes cerrado o permitir con periodo distinto?

2. Implementar validación de periodo cerrado en `VentasPOS.razor.cs`, `Facturacion.razor.cs`, `NotasCredito.razor.cs`

3. Si se bloquea: mensaje claro al usuario y opción de reabrir periodo como Admin

4. Proteger también: entradas manuales de inventario, cambios/eliminaciones de gastos contabilizados

5. Smoke manual con PostgreSQL productiva: cerrar mes, intentar asiento (bloqueado), intentar venta (bloqueada), reabrir como Admin

**Archivos afectados:** `VentasPOS.razor.cs`, `Facturacion.razor.cs`, `NotasCredito.razor.cs`, `CuentasContablesService.cs`, `PeriodoContableService.cs` **Esfuerzo estimado:** 1-2 días


#### T-03. Adjuntar PDF del CFDI al envío de correo (§2.2 / T-03)

**Estado:** `EnviarCfdiAsync` adjunta **solo el XML**. Falta generar el PDF del CFDI (ya existe `QRCoder` y `QuestPDF` en el proyecto) y adjuntar ambos al correo.

**Qué falta exactamente:**

1. Implementar `ExportacionService.FinancieroPdfAsync` o generar PDF de factura CFDI con representación impresa formal (QR SAT, sello, cadena original)

2. Modificar `EnviarCfdiAsync` para adjuntar XML + PDF (defensivo: si falla PDF, enviar solo XML y notificar)

3. Definir si el PDF es obligatorio: si es obligatorio, no dar envío como exitoso cuando falte el PDF

4. Botón manual en `Facturacion.razor` ya existe; solo falta el PDF adjunto

**Archivos afectados:** `EmailService.cs`, `ExportacionService.cs`, `Facturacion.razor.cs`, `IExportacionService.cs` **Esfuerzo estimado:** 1-2 días


#### T-04. Ampliar `AuditLog` a acciones críticas de UI (§3 / T-04)

**Estado:** `AuditLog` ya registra: `PasswordChange`, `DosFa\*`, `RfcValidated`, `FiscalSituationChanged`, `PasswordPolicyChanged`, y transiciones de OV (confirmar, suspender, reanudar, cancelar, facturar con `usuario\_id`, motivo, estados anterior/nuevo).

**Qué falta exactamente:**

1. Auditar: crear/editar producto y precios

2. Auditar: compra (crear, recibir, cancelar)

3. Auditar: ajuste de almacén

4. Auditar: cambios de configuración (incluida política de contraseña)

5. Auditar: aperturas y cierres de caja

6. Registrar `usuario\_id`, fecha, tabla afectada, registro ID, valores anterior y nuevo

**Archivos afectados:** `AuditLog` entidad, `AuditoriaService`, `Productos.razor.cs`, `Compras.razor.cs`, `Almacen.razor.cs`, `Configuracion.razor.cs`, `CorteCaja.razor.cs` **Esfuerzo estimado:** 2-3 días


#### T-05. Pruebas externas reales: Finkok TEST + CSD + SAT (§6.3 / T-05)

**Estado:** El código soporta modo SIMULADO/TEST/PRODUCCIÓN. Timbrado PUE exitoso contra Finkok TEST (UUID obtenido). Pero **no se han probado en vivo**: PPD + REP, cancelación, `EstadoSatAsync` (consulta SAT), y no hay CSD + credenciales configuradas en `user-secrets`.

**Qué falta exactamente:**

1. Colocar `certs/csd.cer` + `certs/csd.key` (nunca al repo)

2. Configurar `dotnet user-secrets`: `Cfdi:KeyPassword`, `Cfdi:\_\_Pac\_\_Usuario`, `Cfdi:\_\_Pac\_\_Password`, `Cfdi:PacModo=TEST`

3. Timbrar factura PPD con complemento de pago REP contra Finkok TEST

4. Cancelación con motivo 02

5. `EstadoSatAsync` vía `get\_sat\_status` (consulta al SAT)

6. Validar que `UI /facturacion` muestre el modo PAC real

7. Guía operativa: `docs-validacion-finkok-test.md`

**Archivos afectados:** `certs/` (nueva carpeta), `user-secrets`, `PacTimbradoService.cs`, `Facturacion.razor.cs` **Esfuerzo estimado:** 2-3 días (depende de disponibilidad de CSD y credenciales)


#### T-06. Validación SMTP real de extremo a extremo (§2.2 / T-06)

**Estado:** `EmailService` (MailKit) con `ProbarAsync`, `EnviarCfdiAsync`, estados de cuenta, comunicados, `ValidarListoParaEnviar` fail-closed — todo implementado en código pero **no validado con servidor SMTP real**.

**Qué falta exactamente:**

1. Configurar servidor/cuenta SMTP de prueba

2. Enviar CFDI (XML + PDF), estado de cuenta CxC/CxP, comunicado

3. Verificar recepción, destinatarios correctos y adjuntos XML/PDF

4. Definir comportamiento si falla generación de PDF: ¿obligatorio o no?

5. Guía operativa: `docs-validacion-smtp.md`

**Archivos afectados:** `EmailService.cs`, configuración SMTP en `user-secrets`/`appsettings` **Esfuerzo estimado:** 1-2 días (depende de disponibilidad de servidor SMTP)


### PRIORIDAD 1 — INTEGRIDAD CONTABLE Y DATOS

#### T-07. Decidir tratamiento de ventas en periodo cerrado (P1-1)

**Definir regla de negocio:** impedir ventas/notas de crédito/operaciones que cambien pólizas en mes cerrado; vía de corrección = reabrir periodo como Admin. Cotizaciones y órdenes sin impacto contable siguen permitidas hasta facturación.

**Esfuerzo estimado:** 0.5 días (decisión + documentación)


#### T-08. Completar trazabilidad de operaciones sensibles (P1-2)

**Ya parcialmente hecho** (ver T-04). Queda confirmar que todos los eventos de auditoría se registran con usuario autenticado y valores anterior/nuevo.

**Esfuerzo estimado:** Incluido en T-04


#### T-09. Validar manualmente flujos de seguridad y operación ya implementados (P1-4)

Recorridos manuales con UI y datos de prueba:

- Login, 2FA, vencimiento de contraseña

- Alta/edición de cliente, búsqueda global

- Escaneo de códigos de barras en POS

- Cierre/reapertura de comisiones

- Conciliación bancaria

- Cotización → OV → factura

- Cierre contable desde UI (`/reportes` → pestaña Cierre contable)

- Reportes financieros desde UI (conciliación visual)

**Esfuerzo estimado:** 2-3 días


#### T-10. Documentar alcance de reportes históricos (P1-4)

Las facturas antiguas sin póliza no se retrocontabilizan; los reportes basados en el libro no incluyen esos ingresos. Falta comunicar esta diferencia en la UI.

**Esfuerzo estimado:** 0.5 días


### PRIORIDAD 2 — MANTENIBILIDAD Y CONSISTENCIA (deuda técnica)

#### T-11. Extraer literal `"Admin"` a constante/enum + helper `EsAdmin(ctx)` (§6.2 / A4)

**Hay 81 comparaciones** del literal `"Admin"` en `.cs` y `.razor.cs` (servicios + páginas). No existe `const string Admin`, ni enum, ni helper centralizado.

**Qué falta exactamente:**

1. Crear `Roles.Admin` (constante o enum) en `Domain/Services/Roles.cs`

2. Reemplazar los 81 usos de `ctx.Rol == "Admin"` por `Roles.Admin` / `EsAdmin(ctx)`

3. Crear helper `EsAdmin(SucursalContext ctx)` en un solo lugar

**Archivos afectados:** `Roles.cs`, ~81 archivos con `== "Admin"` **Esfuerzo estimado:** 2-3 días


#### T-12. Unificar resolución de sucursal en helper único (§6.2 / B8)

**Patrón duplicado en 15 sitios de ~12 servicios:** `ctx.Rol == "Admin" ? sucursalId : ctx.SucursalId`

**Qué falta exactamente:**

1. Crear `ResolverSucursalAutorizada(...)` en `ISucursalContext` o servicio de autorización

2. Reemplazar los 15 duplicados en: `FacturacionService.cs:21`, `ComprasService.cs:16`, `CorteCajaService.cs:19,30`, `ComplementoPagoService.cs:20`, etc.

**Archivos afectados:** `SucursalContext.cs`, ~12 servicios **Esfuerzo estimado:** 1-2 días


#### T-13. Llevar catálogos SAT a tabla `catalogos\_sat` y que todas las páginas consuman el servicio (§6.2 / A3)

**Estado:** `CatalogosSatService` tiene diccionarios hardcodeados en código (no tabla). Consumo inconsistente: POS/Clientes/ComplementosPago usan el servicio, pero `Compras.razor:250,252`, `Facturacion.razor:151`, `NotasCredito.razor:113` siguen con literales HTML.

**Qué falta exactamente:**

1. Crear tabla `catalogos\_sat(catalogo, clave, descripcion, grupo, activo)` + seed

2. Migrar `CatalogosSatService` a leer de tabla (mantener interfaz `ICatalogosSatService`)

3. Reemplazar literales en `Compras.razor`, `Facturacion.razor`, `NotasCredito.razor`, `Clientes.razor`, `Proveedores.razor`

4. Eliminar `switch` de agrupación de formas de pago en `CorteCajaService.Cierre.cs:79-85` (usar campo `grupo`)

**Archivos afectados:** `CatalogosSatService.cs`, `ContextoCatalogos.cs`, `Compras.razor`, `Facturacion.razor`, `NotasCredito.razor`, `Clientes.razor`, `Proveedores.razor`, `CorteCajaService.Cierre.cs`, migración EF **Esfuerzo estimado:** 2-3 días


#### T-14. DIP: `Metas.razor.cs` y `VentasPOS.razor.cs` inyectan `ErpDbContext` directo (§6.2)

Únicos dos componentes Blazor que aún inyectan `ErpDbContext` directo en lugar de servicios.

**Qué falta exactamente:**

1. Mover lógica a `IComisionesService` / `IVentasService`

2. Eliminar inyección directa de `ErpDbContext` en páginas Blazor

**Archivos afectados:** `Metas.razor.cs`, `VentasPOS.razor.cs` **Esfuerzo estimado:** 0.5-1 día


#### T-15. ISP: dividir `ICotizacionesService` y `IVentasService` (§6.2)

- `ICotizacionesService` (~20 métodos) → `ICotizacionesConsulta` + `ICotizacionesOperaciones`

- `IVentasService` (mezcla POS + despacho + devoluciones + auditoría) → `IVentasPosService` / `IEntregaAlmacenService` / `IHistorialVentasService`

**Archivos afectados:** `ICotizacionesService.cs`, `IVentasService.cs`, implementaciones y consumidores **Esfuerzo estimado:** 1-2 días


#### T-16. Corregir parámetros ignorados en asientos de compra/devolución (§6.2)

`IAsientoGeneradorService.GenerarAsientosDeCompraAsync` y `GenerarAsientosDeDevolucionAsync` ignoran `sucursalId`/`usuarioId`.

**Esfuerzo estimado:** 0.5 día


#### T-17. REGLA \#1 en lecturas de cotización y OV (§6.2)

`ObtenerCotizacionAsync` y `ObtenerOrdenVentaAsync` no filtran por sucursal (las 4 transiciones ya lo tienen).

**Esfuerzo estimado:** 0.5 día


#### T-18. Atomicidad en `ConvertirOrdenAVentaAsync` (§6.2)

Envolver en `BeginTransactionAsync` — hoy se crean factura/detalle/stock/kardex sin envolvente transacional.

**Esfuerzo estimado:** 0.5-1 día


#### T-19. Consistencia de estados: homogeneizar string vs enum (§6.2 / A9)

`EstadoFactura` enum ya existe y se usa en `FacturacionService`/`GestionService`, pero quedan literales aislados (p. ej. `ComplementoPago.cs:46`).

**Esfuerzo estimado:** 0.5 día


#### T-20. DRY de `CalcularTotales` (§6.2)

Se invoca ×3 en creación de cotización → un único punto de cálculo.

**Esfuerzo estimado:** 0.5 día


### PRIORIDAD 3 — FUNCIONALIDAD COMERCIAL OPCIONAL (diferenciadores del giro)

Estos no bloquean el "ERP completo" pero son valiosos para el negocio de lubricantes.

#### T-21. §4.1 Límites de crédito — UI y avisos

Los campos `LimiteCredito` y `DiasCredito` ya existen en `Cliente.cs`, pero falta:

- UI en `Clientes.razor` para administrar límite y días de crédito

- Avisos en búsqueda global y cotizaciones cuando cliente tiene límite

**Esfuerzo estimado:** 1 día


#### T-22. §4.2 Listas de precios y descuento por volumen

**No existen** entidades `ListaPrecio`, `ListaPrecioDetalle`. Hoy solo `PrecioVenta`/`PrecioMayoreo`/`PrecioUnitario` en `Producto`.

**Qué falta exactamente:**

1. Entidades `ListaPrecio` + detalle (Público, Taller, Refaccionaria, Mayorista)

2. Matriz de descuento por volumen (caja/tambor/escala)

3. Aplicación en POS, `CotizacionesService.Crear` y `CalcularTotales`

4. UI: catálogo de listas + asignación por cliente/tipo de cliente

**Archivos afectados:** nuevas entidades, `Producto.cs`, `CotizacionesService.cs`, `VentasPOS.razor.cs`, nuevas páginas **Esfuerzo estimado:** 3-4 días


#### T-23. §4.3 Guía de lubricación / compatibilidad de aceites

**No existen** `VehiculoModelo` ni `AceiteCompatibilidad`.

**Qué falta exactamente:**

1. Entidades: marca, modelo, año, motor, viscosidad recomendada, norma OEM, capacidad de cárter

2. Búsqueda "¿Qué aceite lleva un Versa 2020?" en `VentasPOS.razor.cs` y `CotizacionForm`

3. Seed del parque vehicular común en México

**Esfuerzo estimado:** 2-3 días


#### T-24. §4.4 Evaluación de proveedores

Calificación de recepciones en `Compras` (cumplimiento, retrasos, bonos/nivel de servicio) + reporte simple.

**Esfuerzo estimado:** 2 días


#### T-25. §5 Logística de lubricantes (4 ítems, todos 0 %)

**No existen** estas entidades ni funcionalidades:

| Ítem | Qué falta |
| - | - |
| Tambor abierto / granel | Diferenciar existencia sellada vs. fraccionada; `InventarioLote` solo descuenta litros |
| Rutas y reparto | Entidades `RutaEntrega`, `VehiculoReparto`, `Chofer`; hoja de ruta, remisión, liquidación |
| Envases retornables | Control de tambores 208 L prestados, depósito/bonificación por devolución |
| Carta Porte 3.x | Servicio + UI para CFDI de traslado (hoy solo XSLTs genéricos) |


**Esfuerzo estimado:** 5-8 días (todos juntos)


### PRIORIDAD 4 — DOCUMENTACIÓN Y CIERRE

#### T-26. Actualizar README.md con módulos nuevos (§8)

**Esfuerzo estimado:** 0.5-1 día

#### T-27. Decidir commit de docs untracked (§8)

Varios .md y .docx están sin trackear en git.

**Esfuerzo estimado:** 0.5 día


## 📈 TABLA RESUMEN DE PORCENTAJES POR ÁREA

| Área | Completado | Faltante | Notas |
| - | - | - | - |
| Autenticación y seguridad | 100 % | — | Login, 2FA, password policy OK |
| Clientes / Proveedores | 100 % | — | Multi-correo, teléfonos, direcciones OK |
| Productos / Almacén / Inventario | 100 % | — | Kardex, lotes, códigos de barras OK |
| Ventas (POS, Cotización, OV, Factura) | 95 % | 5 % | Falta bloqueo por morosidad |
| Compras | 95 % | 5 % | Falta evaluación de proveedores |
| CFDI / Facturación electrónica | 85 % | 15 % | Falta PPD+REP, cancelación, EstadoSatAsync, PDF adjunto |
| Contabilidad / Cierre / FIFO | 90 % | 10 % | Falta bloqueo de ventas en periodo cerrado, unificación ER |
| Caja / Bancos / Conciliación | 100 % | — | OK |
| Reportes financieros | 90 % | 10 % | Falta exportación PDF de reportes financieros, verificación UI |
| CxC / CxP | 95 % | 5 % | Falta UI completa de límites de crédito |
| Auditoría y permisos | 85 % | 15 % | Falta ampliar AuditLog a UI crítica |
| Comisiones | 100 % | — | OK |
| Configuración | 100 % | — | OK |
| Exportación Excel/PDF | 95 % | 5 % | Falta PDF del CFDI al correo |
| Logística (rutas, envases, Carta Porte) | 0 % | 100 % | Opcional |
| Comercial (listas precios, lubricación) | 10 % | 90 % | Opcional |
| Deuda técnica (SOLID/DRY/hardcode) | 70 % | 30 % | Falta "Admin" constante, helper sucursal, catálogos SAT tabla |
| Pruebas externas (Finkok/SMTP) | 40 % | 60 % | Falta validación en vivo |
| Documentación | 70 % | 30 % | Falta README, smoke tests manuales |


**Ponderado total (ERP completo): ~85 % completado / ~15 % faltante**


## 🎯 PLAN DE ATAQUE SUGERIDO (orden de impacto)

### Semana 1 — Bloqueantes críticos (T-01 a T-06)

1. **T-01** Bloqueo por morosidad

2. **T-02** Cierre de periodo — bloquear ventas en periodos cerrados

3. **T-03** Adjuntar PDF del CFDI al correo

4. **T-04** Ampliar `AuditLog` a acciones críticas de UI

5. **T-05** Pruebas Finkok TEST (PPD+REP, cancelación, EstadoSatAsync) — *depende de CSD*

6. **T-06** Pruebas SMTP real — *depende de servidor SMTP*

### Semana 2 — Integridad contable y verificación (T-07 a T-10)

1. **T-07** Decidir y documentar tratamiento de ventas en periodo cerrado

2. **T-08** Confirmar trazabilidad de auditoría (incluido en T-04)

3. **T-09** Smoke manual de todos los flujos con UI + PostgreSQL

4. **T-10** Documentar alcance de reportes históricos

### Semana 3 — Deuda técnica de alto impacto (T-11 a T-13)

1. **T-11** Extraer `"Admin"` a constante/enum (81 sitios)

2. **T-12** Helper único de resolución de sucursal (15 duplicados)

3. **T-13** Catálogos SAT a tabla `catalogos\_sat` + consumir en todas las páginas

### Semana 4 — Deuda técnica menor y opcional (T-14 a T-27)

1. **T-14** DIP en `Metas.razor.cs` / `VentasPOS.razor.cs`

2. **T-15** ISP en `ICotizacionesService` / `IVentasService`

3. **T-16** Parámetros ignorados en asientos

4. **T-17** REGLA \#1 en lecturas

5. **T-18** Atomicidad `ConvertirOrdenAVentaAsync`

6. **T-19** Consistencia estados enum/string

7. **T-20** DRY `CalcularTotales`

8. **T-21 a T-27** Opcionales según prioridad del negocio


## ⚠️ DEPENDENCIAS EXTERNAS (bloqueantes no técnicos)

Estas tareas **no pueden completarse** sin recursos externos que el proyecto no controla:

| Tarea | Recurso necesario | Estado |
| - | - | - |
| T-05 Finkok TEST en vivo | CSD (.cer + .key), credenciales PAC de prueba | Pendiente de entrega |
| T-06 SMTP real | Servidor/cuenta SMTP de prueba | Pendiente de configuración |
| T-09 Smoke PostgreSQL productiva | Base de datos de prueba con datos realistas | Pendiente |



## ✍️ CONCLUSIÓN

El ERP OilsMexico está **~85 % completo**. El núcleo operativo (ventas, compras, almacén, CFDI PUE, contabilidad, caja, reportes, comisiones, permisos) está funcionando y compila con 0 errores.

El **~15 % restante** se concentra en:

1. **Bloqueantes funcionales** (morosidad, cierre de periodo, PDF adjunto al correo, auditoría extendida) — ~40 % del faltante

2. **Validaciones externas** (Finkok PPD+REP/cancelación/SAT, SMTP real) — ~25 % del faltante

3. **Deuda técnica** ("Admin" constante, helper sucursal, catálogos SAT tabla) — ~20 % del faltante

4. **Opcionales comerciales y logísticos** (listas precios, lubricación, rutas, envases, Carta Porte) — ~15 % del faltante

Con 2-3 semanas de trabajo enfocado (sin contar dependencias externas de CSD y SMTP), el ERP puede considerarse 100% completo para operación productiva.


lee el archivo: "Checklist completitud ERP.md" y dime la prioridad de lo que falta por implementar

Ya leí el archivo completo. Aquí está el **estado de lo pendiente, ordenado por prioridad** según la propia leyenda del checklist (🔴 alta · 🟡 media · 🟢 baja · 🔵 verificación):


## 🔴 PRIORIDAD ALTA — §1 Funcionalidades nuevas (restos de secciones casi terminadas)

Lo grueso de §1 ya está implementado (1.1 Gastos ✅, 1.2 CxC/CxP ✅, 1.3 FIFO+cierre ✅, 1.4 Reportes ✅), pero quedan 7 pendientes:

1. **Bloqueo por morosidad en POS/Facturación** cuando el saldo vencido supere el límite (§1.2) — *depende de §4.1 (límites de crédito)*.

2. **Verificación con BD de CxC/CxP**: backfill de esquema, vencimientos, pagos parciales y tasas en PostgreSQL.

3. **Decisión de diseño en §1.3**: POS y Notas de Crédito **no validan periodo cerrado** (solo se bloquean asientos) — decidir si se bloquean ventas en meses cerrados o se recomputa al reabrir.

4. **Smoke manual del cierre contable con PostgreSQL productiva** (`PeriodoContablePatch`, cerrar mes, bloqueo, reapertura) — los tests InMemory dieron 10/10.

5. **Verificación con BD de los 5 reportes financieros** (cuadre de Balanza, Balance, ER vs. utilidad, existencias vs. kardex).

6. **§1.4b — unificar la fuente del Estado de Resultados**: ingresos y gastos aún se calculan desde `Facturas`/`Producto` en vez de los asientos contables (el costo de ventas ya cuadra desde rev.4).


## 🟡 PRIORIDAD MEDIA — §2, §3, §6, §8

**§2 Exportación/documentos** (todo implementado; solo verificación):

- Verificación manual con **SMTP real** (adjunto PDF del CFDI llega bien) y con **PostgreSQL productivo**.

**§3 Auditoría y permisos — los 5 ítems están 100% pendientes:**

1. UI de matriz permisos × roles (las tablas existen, no hay interfaz ni consumo).

2. `IAutorizacionService.TienePermiso` aplicado en `NavMenu` y páginas críticas (hoy solo ~15 filtros de rol duro).

3. Ampliar `AuditLog` a crear/editar producto, precios, compras, ajustes, config, cierre de caja, transiciones de Cotizaciones/OV.

4. Registrar `usuario\_id` de quién confirma/suspende/reanuda/cancela en OV.

5. Pantalla de consulta de auditoría (filtros fecha/usuario/acción).

**§6 Deuda técnica (~17 ítems, esfuerzo bajo → alto impacto):**

- **6.1 Hardcode en UI** (5): `Usuarios.razor` roles, `Compras.razor` formas de pago, motivos de cancelación duplicados, IVA/módulos futuros en `Home.razor`, decisión de catálogos SAT a tabla.

- **6.2 SOLID/DRY** (10): helper único de sucursal B8 (~15 sitios), DIP (`Metas`/`VentasPOS` inyectan `ErpDbContext`), ISP de `ICotizacionesService` y `IVentasService`, parámetros ignorados en asientos de compra/devolución, REGLA \#1 en lecturas de cotización/OV, atomicidad de `ConvertirOrdenAVentaAsync`, consistencia de estados, DRY de `CalcularTotales`, enum `EstadoFactura`.

- **6.3 CFDI/PAC modo real** (2): colocar certs CSD + user-secrets, prueba Finkok TEST (PUE, PPD+REP, cancelación, `EstadoSatAsync`).

**§8 Documentación** (2): actualizar `README.md` con módulos nuevos; decidir commit de docs untracked.


## 🟢 PRIORIDAD BAJA — §4 y §5 (diferenciadores opcionales, no bloquean "ERP completo")

**§4 Comercial y crédito (10 ítems):**

- **4.1** `Cliente.LimiteCredito` + bloqueo automático POS/facturación + avisos.

- **4.2** Entidades `ListaPrecio` + descuento por volumen + UI de catálogos.

- **4.3** Guía de lubricación/compatibilidad (`VehiculoModelo`, `AceiteCompatibilidad`, búsqueda "¿qué aceite lleva un Versa 2020?").

- **4.4** Evaluación de proveedores.

**§5 Logística de lubricantes (4 ítems):**

- Tambor abierto/granel (existencia sellada vs. en uso) · Rutas y reparto (`RutaEntrega`, `VehiculoReparto`, `Chofer`) · Envases retornables (depósito 208 L) · Carta Porte 3.x.


## 🔵 §7 Verificación manual pendiente (9 ítems, con BD levantada)

2FA, RFC duplicado/cancelado, escaneo de barras, cierre de comisión, conciliación en UI, smoke de Cotizaciones/OV, timbrado Finkok, reportes financieros con BD, cierre de periodo con BD.


## Orden de ataque recomendado (el que fija el propio documento)

> **1 → 2 → 3**, atacando **§6 en paralelo** (6.1 → 6.2 → 6.3 por ser esfuerzo bajo y alto impacto). **§4 y §5 son opcionales** para considerar el ERP "completo", valiosos para el negocio.

### Observación: 3 ítems del checklist parecen desactualizados

- Línea 63: dice falta exportación Excel/PDF de reportes, pero §2.1/§9 marca `FinancieroPdfAsync` como **hecho**.

- Línea 110: dice que solo se adjunta el XML, pero §9 (línea 88) confirma que `EnviarCfdiAsync` ya adjunta **XML + PDF**.

- Línea 130: dice que `Cliente.DiasCredito` no existe, pero §1.2 (línea 30, verificado hoy) indica que **ya existe** con default 0.

Conviene limpiar esos 3 renglones para que el checklist refleje el estado real. Si quieres, en modo Act puedo actualizar el archivo o atacar alguno de los pendientes de prioridad alta.


Voy a releer el checklist actualizado y ordenar únicamente lo pendiente por prioridad, distinguiendo bloqueantes funcionales, seguridad/operación y mejoras opcionales.

Leído el `Checklist completitud ERP.md`. Recomiendo priorizar lo pendiente así:

| Prioridad | Qué atender | Por qué |
| - | - | - |
| **P0 — Seguridad** | Completar la matriz de permisos por rol y aplicarla en el menú y páginas críticas (§3). | Las tablas existen, pero mientras no se apliquen, el acceso depende de guards fijos dispersos. Al sustituirlos, conservar una matriz inicial equivalente a los accesos actuales para no alterar permisos por accidente. |
| **P1 — Integridad operativa y contable — implementado** | La conversión OV → venta es transaccional; las ventas nuevas generan póliza y las notas de crédito revierten la original; el libro alimenta los ingresos del Estado de Resultados. | La transacción incluye factura, inventario y vínculo con OV. El registro contable respeta el bloqueo de periodos cerrados. Por decisión de alcance, las facturas históricas sin póliza no se modifican ni se retrocontabilizan. |
| **P1 — Crédito y cobranza — implementado** | `Cliente.LimiteCredito` se administra desde la UI; el servicio valida crédito PPD y saldo vencido contra el límite antes de vender. | Evita depender solo de validaciones del cliente; el comportamiento está cubierto en pruebas PostgreSQL. |
| **P1 — Verificación sobre PostgreSQL — automatización implementada** | Las pruebas PostgreSQL cubren CxC/CxP, parches, cierre/reapertura, Balanza, Estado de Resultados, existencias, crédito y atomicidad OV → venta. | La suite de integración completa aprobó 35/35; las pruebas PostgreSQL focalizadas aprobaron 7/7. Esto no sustituye las verificaciones manuales de §7 ni las pruebas externas SMTP/PAC. |
| **P2 — Operación externa** | Ejecutar pruebas de SMTP real y el flujo Finkok TEST —PUE, PPD/REP, cancelación y consulta SAT— cuando estén disponibles CSD y credenciales seguras (§2.1, §2.2 y §6.3). | Son validaciones importantes antes de operar correo y timbrado reales, pero dependen de configuración y certificados externos. |
| **P2 — Trazabilidad restante** | Ampliar auditoría a productos/precios, compras, ajustes de almacén, configuración y cierre de caja (§3). | Ya se contempla la auditoría de transiciones de OV, pero todavía falta cubrir otras operaciones relevantes. |
| **P3 — Mejoras y mantenimiento** | Completar listas de precios y descuentos por volumen (§4.2); luego compatibilidad de aceites (§4.3), evaluación de proveedores (§4.4) y deuda técnica de menor impacto (§6). | Aportan valor comercial y mantenibilidad, pero no son tan urgentes como seguridad, integridad contable y crédito. |
| **P4 — Ampliaciones logísticas y documentación** | Tambor abierto/granel, rutas, envases retornables y Carta Porte (§5); actualizar el README (§8). | El propio checklist trata logística como opcional para alcanzar “ERP completo”; la documentación puede hacerse después de estabilizar el alcance funcional. |

**Alcance histórico de P1:** no se hará backfill automático de ventas anteriores que carecen de póliza, para preservar periodos cerrados. Por ello, el Estado de Resultados basado en el libro no incluye esos ingresos históricos sin asiento; las ventas nuevas sí quedan contabilizadas.

**Antes de tomar la lista como fuente definitiva**, conviene sincronizarla con el código: vi pendientes duplicados o contradictorios —por ejemplo, exportación y PDF del CFDI aparecen como hechos en §2.1, pero también como pendientes en otras secciones— y varios ítems de §6 ya estaban en cambios de trabajo previos. Además, la verificación PostgreSQL debe hacerse en una base de prueba, no directamente en producción.


## revisa porque a veces no puedo abrir un menú, por ejemplo: Contactos después de haber abierto configuración con el usuario administrador y corrígelo


Corregí el acordeón de `NavMenu.razor`: ahora cada grupo puede abrirse sin cerrar el anterior. Así, **Contactos** permanece accesible aunque **Configuración** esté abierto; hacer clic de nuevo en un grupo lo cierra, y al navegar se abre automáticamente el grupo correspondiente a la página.

**Validación:** compilación correcta y `git diff --check` limpio. La compilación se hizo en una carpeta temporal porque la instancia activa del ERP tenía bloqueado el archivo de salida.

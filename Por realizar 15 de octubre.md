Por realizar 15 de octubre (por Copilot)


### revisa el archivo: "Checklist completitud ERP.md" y "por implementar 14 octubre 2026.md" y dame una lista por importancia de lo que falta por implementar o mejorar, dame detalles de cada punto o tarea

Voy a leer ambos documentos y contrastar sus listas para consolidar pendientes, priorizarlos por impacto y detallar el trabajo que implica cada uno.

Find files matching `\*\*/\{Checklist completitud ERP.md,por implementar 14 octubre 2026.md\}`

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Checklist completitud ERP.md" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\por implementar 14 octubre 2026.md" \}

Read , lines 1 to 140 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Checklist completitud ERP.md", "view\_range": \[ 1, 140 \] \}

Read , lines 1 to 160 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\por implementar 14 octubre 2026.md", "view\_range": \[ 1, 160 \] \}

Read , lines 48 to 140 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Checklist completitud ERP.md", "view\_range": \[ 48, 140 \] \}

Read , lines 141 to 238 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Checklist completitud ERP.md", "view\_range": \[ 141, 238 \] \}

Read , lines 78 to 115 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\por implementar 14 octubre 2026.md", "view\_range": \[ 78, 115 \] \}

Read , lines 118 to 190 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Checklist completitud ERP.md", "view\_range": \[ 118, 190 \] \}

Read , lines 191 to 238 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Checklist completitud ERP.md", "view\_range": \[ 191, 238 \] \}

Read , lines 1 to 77 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\por implementar 14 octubre 2026.md", "view\_range": \[ 1, 77 \] \}


Revisé los dos documentos. La lista siguiente separa lo que **parece realmente pendiente** de lo que los propios archivos marcan como completado; no hice una auditoría del código, así que me baso en lo que registran.

## Prioridad 0 — Validaciones antes de operar en producción

> **Revisión verificada 15-oct-2026:** `dotnet build OilsMexico.slnx --no-restore`
> terminó con 0 errores y 0 advertencias; la suite completa terminó con 36/36
> pruebas aprobadas (incluye PostgreSQL/Testcontainers). Corrección incluida:
> `BalanceAsync` descuadraba porque el resultado se calculaba con signo
> invertido (`gas − ing` en vez de `ing − gas`) y las cuentas se presentaban
> sin normalizar a su naturaleza — corregido y cubierto con prueba nueva.

1. **Probar timbrado real en Finkok TEST — ⏳ BLOQUEADO (falta CSD + credenciales).**
Recorrer los casos indicados: factura PUE, factura PPD con complemento de pago REP, cancelación y consulta del estado ante el SAT. Confirmar que certificados, credenciales y configuración estén cargados mediante `user-secrets` o un mecanismo seguro, nunca en el repositorio. Esto depende de disponer del CSD y credenciales de prueba.
   - Verificado por revisión de código: `PacTimbradoService` con modos SIMULADO/TEST/PRODUCCION,
     `DatosPac` fail-closed sin credenciales, reintento Finkok 307, `cancel_signature`
     firmada local (la llave nunca viaja), `EstadoSatAsync` vía `get_sat_status`,
     UI `/facturacion` muestra el modo PAC. Sin CSD/credenciales opera en SIMULADO.
   - **No validado en vivo:** no se probaron PUE, PPD + REP, cancelación ni consulta SAT
     contra Finkok TEST. La existencia del soporte en código no acredita respuesta del PAC.
   - Guía operativa: `docs-validacion-finkok-test.md`.

2. **Validar el correo SMTP de extremo a extremo — ⏳ BLOQUEADO (falta servidor SMTP).**
Probar la conexión y el envío real de CFDI, estados de cuenta y comunicados. Verificar recepción, destinatarios y adjuntos XML + PDF. Los documentos indican que el PDF al correo ya está implementado; lo pendiente es comprobar el flujo con un servidor SMTP real.
   - Verificado por revisión de código: `EmailService` (MailKit) con `ProbarAsync`,
     `EnviarCfdiAsync` adjunta XML + PDF (defensivo: si falla el PDF igual va el XML),
     estados de cuenta CxC/CxP, comunicados y `ValidarListoParaEnviar` fail-closed.
   - **Salvedad de aceptación:** si la generación del PDF falla, el código captura la
     excepción y puede enviar el correo solo con XML. El requisito XML + PDF no queda
     garantizado en ese caso; el flujo también informa que se envió solo XML. Definir si
     el PDF es obligatorio y, de serlo, no dar el envío por exitoso cuando falte.
   - **No validado en vivo:** faltan servidor SMTP, envío a buzón de prueba y verificación
     de recepción, destinatarios y adjuntos.
   - Guía operativa: `docs-validacion-smtp.md`.

3. **Validar cierre contable con PostgreSQL — ✅ AUTOMATIZADO / ⏳ SMOKE UI.**
En una base de prueba, comprobar cierre de un mes con ventas, póliza de costo, bloqueo de nuevos asientos en el periodo y reapertura con el usuario Admin. Las pruebas automatizadas se describen como aprobadas, pero falta el smoke manual contra PostgreSQL.
   - Verificado en esta revisión: `PostgreSQL_conciliacion_cierre_balance_resultados_y_existencias`
     pasó dentro de la suite (Testcontainers con BD desechable). El escenario registra compra
     de 100 L y venta de 50 L con movimientos y pólizas, cierra con costo FIFO de $500,
     verifica el bloqueo de asientos y reabre como Admin.
   - **Alcance de la prueba:** valida servicios y persistencia PostgreSQL; no recorre la UI
     ni ejecuta el flujo de compra/POS de extremo a extremo.
   - Pendiente: smoke manual desde la UI (`/reportes` → pestaña Cierre contable).

4. **Conciliar los reportes financieros — ✅ AUTOMATIZADO / ⏳ SMOKE UI.**
Revisar que el Balance cuadre (Activo = Pasivo + Patrimonio), que la utilidad esperada coincida con Estado de Resultados y que las existencias concuerden con el kardex. No hacerlo directamente en producción.
   - Verificado en esta revisión: la misma prueba aprobada concilia en PostgreSQL — Balance cuadra
     (tras corregir bug de signo en `BalanceAsync`), ER (ventas 1000, IVA 160, costo 500,
     utilidad bruta 500) y existencias (0 + 100 − 50 = 50, concilia kardex y stock).
   - **Alcance de la prueba:** las cifras y aserciones se validan en los servicios sobre
     datos sintéticos; no se ha conciliado visualmente el reporte de la UI.
   - Pendiente: conciliación manual desde la UI con datos de prueba conocidos.

## Prioridad 1 — Definir y proteger la integridad contable y los datos

1. **Decidir qué pasa si se vende en un periodo contable cerrado.**  
El checklist señala que POS y Notas de Crédito no validan el periodo cerrado, aunque el registro de asientos sí se bloquea. Hay que definir la regla de negocio: impedir esas operaciones, permitirlas con un periodo contable distinto, o establecer cómo se corrigen al reabrir. Después, implementar y probar la opción elegida; no dejar la decisión implícita.

2. **Completar la trazabilidad de operaciones sensibles.**  
Añadir eventos de auditoría para altas y cambios de productos/precios, compras, ajustes de almacén, cambios de configuración y cierres de caja. Registrar quién hizo la acción, cuándo, sobre qué registro y —cuando corresponda— los valores anterior y nuevo. Las transiciones de OV y la pantalla de auditoría ya aparecen como completadas.

3. **Revisar los filtros de sucursal en las consultas pendientes.**  
El checklist marca `ObtenerCotizacionAsync` y `ObtenerOrdenVentaAsync` como lecturas que necesitan validar sucursal. Comprobar que el usuario no pueda consultar registros de otra sucursal alterando un identificador o accediendo directamente a una ruta. Mantener las excepciones administrativas según la regla de autorización existente.

4. **Precisar el tratamiento de ventas históricas sin póliza.**  
Un documento señala como decisión acordada no retrocontabilizar facturas antiguas para proteger periodos cerrados; por ello, sus ingresos no aparecen en los reportes basados en el libro. No lo trataría como implementación pendiente mientras esa sea la decisión del negocio. Sí conviene documentar claramente el alcance y confirmar que los usuarios entienden la diferencia en reportes históricos.


## Prioridad 2 — Mantenibilidad y consistencia funcional

1. **Eliminar duplicaciones y accesos directos a datos en la UI.**  
El checklist enumera varios trabajos: centralizar la resolución de sucursal donde aún esté duplicada, sustituir accesos directos a `ErpDbContext` en páginas por servicios, y simplificar el cálculo de totales de cotizaciones. Conviene abordarlos con pruebas de regresión para evitar cambios en permisos, precios o inventario.

2. **Revisar contratos de servicios y estados.**  
Evaluar la separación de interfaces grandes (`ICotizacionesService` y `IVentasService`), eliminar o aprovechar parámetros de asientos que actualmente se describen como ignorados y unificar estados string/enum. Son mejoras de estructura: deben hacerse solo si se confirma que siguen pendientes en el código.

3. **Quitar hardcodes remanentes de la interfaz.**  
El documento señala roles literales en Usuarios, formas de pago en Compras, motivos de cancelación duplicados y textos/IVA fijo en Home. Reutilizar las constantes y catálogos existentes y mostrar el estado real del PAC en vez de etiquetas estáticas. La decisión de pasar catálogos SAT a tablas es una elección de alcance, no una tarea automática.

4. **Validar manualmente los flujos de seguridad y operación ya implementados.**  
El checklist aún marca recorridos de 2FA y vencimiento de contraseña, validación de RFC, lectura de barras, cierre/reapertura de comisiones, conciliación bancaria y Cotización → OV. Son pruebas de aceptación manual, no necesariamente funciones nuevas: los documentos afirman que varias ya tienen cobertura automatizada o están implementadas.

## Prioridad 3 — Funcionalidad comercial opcional

1. **Listas de precios y descuentos por volumen.**  
Crear listas diferenciadas (por ejemplo, público, taller, refaccionaria y mayorista), relacionarlas con clientes o tipos de cliente y aplicar reglas por presentación/volumen tanto en POS como en cotizaciones. Añadir administración y pruebas para que el precio, descuento e impuestos coincidan en ambos flujos.

2. **Guía de lubricación y compatibilidad.**  
Modelar vehículo, año, motor, viscosidad, norma OEM y capacidad; permitir buscar una recomendación desde POS y cotizaciones. Hace falta definir una fuente confiable y un proceso para mantener esos datos, además de cargar el catálogo inicial.

3. **Evaluación de proveedores.**  
Capturar cumplimiento, retrasos y nivel de servicio al recibir compras, y ofrecer un reporte para comparar proveedores. Acordar antes cómo se calcula la calificación y cómo se tratan las recepciones parciales o incidencias.

## Prioridad 4 — Logística y documentación

1. **Control de tambor abierto y granel.**  
Distinguir inventario sellado del volumen restante en un envase abierto y evitar vender una unidad cerrada cuando solo queda saldo fraccionado. Requiere acordar cómo se registran apertura, merma y fraccionamiento.

2. **Reparto y rutas.**  
Agregar vehículos, choferes y rutas, con hoja de entrega y liquidación de cantidades entregadas, rechazadas o devueltas. Debe integrarse con inventario y documentos de salida.

3. **Envases retornables.**  
Registrar tambores prestados, depósitos y devoluciones, y reflejar correctamente cargos y abonos en la cuenta del cliente.

4. **Carta Porte 3.x.**  
Implementar el flujo de CFDI de traslado —servicio, validaciones y UI—; el checklist indica que tener XSLT genéricos no cubre todavía ese proceso.

5. **Actualizar el README y sincronizar los pendientes.**  
Documentar los módulos incorporados y depurar el checklist antes de usarlo como backlog. Hay contradicciones entre ambos archivos y dentro del checklist: límites de crédito aparecen como pendientes en una sección, pero como implementados y probados en otra; el PDF del CFDI, la matriz de permisos y la atomicidad de OV → venta también figuran como hechos en unas partes y pendientes en otras. El archivo «por implementar» contiene además texto de conversaciones y tareas anteriores. Conviene consolidar esas decisiones y marcar una sola fuente vigente para evitar reimplementar trabajo ya hecho.

Los pendientes con mayor impacto inmediato son **validar timbrado y SMTP, completar el smoke del cierre contable y definir qué operaciones se permiten en periodos cerrados**. Listas de precios, compatibilidad y logística son ampliaciones valiosas, pero los documentos las clasifican como diferenciadores, no como bloqueantes para completar el ERP.

Documentos revisados: `Checklist completitud ERP.md` y `por implementar 14 octubre 2026.md`.

## Seguimiento — implementación de Prioridad 2 (15 de octubre)

### Cambios de mantenibilidad

- **Acceso a datos de la UI:** eliminado `IVentaEscopoFactory`, que exponía `ErpDbContext` en sus delegados. POS, Metas y Reportes ahora consultan `ICatalogosService` e `IClientesService`; los datos mínimos de clientes para POS se proyectan en `ClienteVentaDto`.
- **Facetas:** las pantallas dependen de las capacidades concretas que utilizan. POS y Compras usan `IVentasPosService`; Facturación e Historial usan `IHistorialVentasService`; Historial inyecta además `IEntregaAlmacenService` para surtir/devolver. Las vistas de solo consulta de cotizaciones y órdenes usan `ICotizacionesConsulta`; formularios y detalles separan consulta de escritura con `ICotizacionesOperaciones`.
- **Resultado:** ya no hay inyecciones de `IVentasService` ni `ICotizacionesService` en las páginas Blazor. Los agregados permanecen como contratos de compatibilidad/implementación para que las facetas sigan compartiendo la misma instancia scoped.

### Validación ejecutada

| Comprobación | Resultado |
|---|---|
| `dotnet build OilsMexico.slnx --no-restore` | Correcto: 0 errores. Queda una advertencia existente de campo no usado `Metas.cerrando` (CS0414). |
| `dotnet test tests\OilsMexico.IntegrationTests\OilsMexico.IntegrationTests.csproj --no-build` | Correcto: 35 pruebas aprobadas, 0 fallidas. |
| Búsqueda de referencias de `IVentaEscopoFactory`, `IVentasService` e `ICotizacionesService` en componentes Blazor | Sin referencias; los componentes tampoco contienen referencias funcionales a `ErpDbContext`. |

### Checklist de aceptación manual y estado

Los siguientes recorridos deben ejecutarse en un entorno de pruebas. **No se consideran aprobados por el build ni por las pruebas automatizadas.** No se ejecutaron aquí porque requieren iniciar sesión en una instancia con datos de prueba y/o acceso a servicios externos; no se deben copiar credenciales ni certificados al repositorio.

| Flujo | Pasos y resultado esperado | Estado |
|---|---|---|
| POS y Compras | Entrar con usuarios autorizados; comprobar que cargan productos, clientes, vendedores y sucursales, que el cliente público se selecciona por RFC y que buscar productos en Compras y registrar una venta siguen funcionando. | Pendiente de recorrido con UI y datos de prueba. |
| Metas y Reportes | Abrir cada pantalla con los roles autorizados; verificar que carguen los vendedores y sucursales, y que reportes conserve la opción de todas las sucursales para Admin/Conta. | Pendiente de recorrido con UI y datos de prueba. |
| Cotizaciones y órdenes | Consultar listas/detalles; crear o editar una cotización; convertirla en orden; confirmar, suspender/reanudar, cancelar y facturar la orden. Verificar también que un usuario no administrador no pueda consultar IDs de otra sucursal. | Pendiente de recorrido con UI y datos de prueba. |
| Historial, surtido y devolución | Consultar el historial, abrir el detalle, surtir y registrar devolución según permisos; confirmar que Facturación puede consultar el detalle sin depender de las operaciones de almacén. | Pendiente de recorrido con UI y datos de prueba. |
| Cierre contable | En PostgreSQL de pruebas, generar pólizas, cerrar un periodo, comprobar el bloqueo de asientos del periodo cerrado y reabrirlo como Admin. Las pruebas automatizadas pasaron; falta validar el recorrido desde la UI. | Automatizado aprobado; manual pendiente. |
| Reportes financieros | Con movimientos de prueba conocidos, conciliar Activo = Pasivo + Patrimonio, el resultado del periodo y existencias contra el kardex. | Pendiente de datos contables de prueba y conciliación. |
| CFDI con Finkok TEST | Configurar CSD y credenciales seguras; probar PUE, PPD + REP, cancelación y consulta de estado ante SAT; verificar XML/PDF descargables. | Pendiente de CSD, credenciales y acceso de sandbox. |
| Correo SMTP | Configurar un servidor/cuenta de prueba; enviar CFDI, estado de cuenta y comunicado; confirmar recepción, destinatarios y adjuntos XML/PDF. | Pendiente de servidor SMTP y credenciales de prueba. |

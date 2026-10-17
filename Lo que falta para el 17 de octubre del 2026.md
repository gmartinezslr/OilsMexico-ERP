Lo que falta para el 17 de octubre del 2026



revisa TODO el proyecto y dame un porcentaje total de lo que falta para terminar el ERP, porcentaje y tareas lo más detalladas posible. Evita lo que ya existe y está funcionando correctamente y no necesita ajustes, cambios o modificaciones



# Evaluación general

**Estimación total pendiente: ~10 %; avance estimado: ~90 %.** Es una estimación por alcance y esfuerzo, no un porcentaje de líneas de código. Los seis diferenciadores ya tienen un MVP persistido e integrado; el saldo pendiente refleja validación real, datos de operación y completar las funciones que deliberadamente quedan como borrador/informativas.

Para el **núcleo operativo del ERP**, la implementación está aproximadamente en **94–96 %**. El trabajo que separa el núcleo de la estimación global corresponde sobre todo a pruebas reales de operación y a funciones especializadas para el negocio de lubricantes que aún no existen.

La estimación **excluye** nómina/RR. HH., producción/MRP, multiempresa y API para terceros, que el proyecto identifica como fuera de alcance. Ya no cuento como pendientes de desarrollo inicial las listas/precios por volumen, compatibilidad, evaluación de proveedores, reparto, tambores/envases ni la captura preliminar de Carta Porte; sus límites para operación de producción se enumeran abajo.

**Verificación realizada:** `dotnet build OilsMexico.slnx --no-restore -v:minimal` terminó correctamente (**0 errores, 0 advertencias**) y `dotnet test OilsMexico.slnx --no-build -v:q` pasó **61/61 pruebas**. Las pruebas de los diferenciadores usan EF InMemory; todavía falta aplicar `SchemaPatch` y probar los flujos de UI/roles contra una base PostgreSQL de prueba, además de validar el proceso fiscal con servicios externos. Esto respalda el estado del código, pero no sustituye recorridos reales de operación.

## Trabajo pendiente estimado

| Prioridad | Área | Pendiente estimado |
| - | - | -: |
| P0 | Pruebas reales de Finkok y SMTP | ~3 % |
| P1 | Recorridos manuales de aceptación con UI y PostgreSQL | ~3 % |
| P1/P2 | Validación y cierre productivo de funciones especializadas | ~4 % |
| P2 | Visibilidad del crédito, advertencia de reportes históricos y documentación | 0 % (completado) |
| **Total estimado** |  | **~10 %** |


### P0 — Completar validaciones de servicios externos

- **Finkok / SAT:** terminar y dejar evidencia de los casos PPD + complemento de pago REP, cancelación —incluido el caso con CFDI sustituto—, consulta de estado SAT y confirmación del modo TEST en Facturación. El documento de trabajo indica que PUE ya se probó, pero la guía de validación todavía no contiene un resultado completo de la batería. No estoy contando como pendiente reescribir el timbrado que ya está implementado. Véase `validación Finkok`.

- **SMTP:** ejecutar desde la UI el envío de prueba, CFDI con XML y PDF, estados de cuenta CxC/CxP y comunicado; comprobar recepción, destinatarios y adjuntos. El PDF obligatorio ya está implementado en `EmailService.Documentos.cs`; falta cerrar la validación de extremo a extremo. Véase `validación SMTP`.

- Mantener CSD y credenciales exclusivamente en configuración local segura; **no subirlos al repositorio**.


### P1 — Recorridos de aceptación manual

Las pruebas automatizadas están pasando; lo pendiente aquí es confirmar que los flujos reales de usuario, la persistencia y los mensajes de la UI cumplen lo esperado:

- Cierre y reapertura de periodo desde Reportes; comprobar en PostgreSQL el asiento, el bloqueo de operaciones dentro del periodo y la reapertura autorizada.

- Reportes financieros: contrastar balance, resultados y existencias con datos conocidos de una sucursal y su kardex.

- Recorrer cotización → orden de venta → factura, además de conciliación bancaria, comisiones y cierre de caja.

- Probar login/2FA, vencimiento de contraseña, validación RFC y escaneo de código de barras con el dispositivo real.

El código ya tiene validaciones de periodo en operaciones relevantes —por ejemplo, en `VentasService.Venta.cs` y `NotaCreditoService.Crear.cs`—, así que **no cuento como pendiente volver a implementarlas**.

### P1/P2 — Diferenciadores implementados como MVP y cierre pendiente

Los seis diferenciadores del alcance original ya cuentan con persistencia, acceso desde la UI y controles del lado del servicio. El acceso a `/diferenciadores` depende del rol; los precios de venta se vuelven a resolver en el servidor al cobrar para no confiar en el importe enviado por la pantalla.

1. **Listas y precio por volumen — MVP implementado.** El administrador configura una lista activa para cada tipo existente (`menudeo`/`mayoreo`) y escalones por SKU. El umbral se expresa en litros equivalentes usando el factor de conversión; la regla se integra al POS y como precio sugerido de cotización. Sin regla aplicable se conserva el precio actual de catálogo y las órdenes facturadas mantienen el precio congelado. Falta probar combinaciones reales de precios/márgenes antes de activar listas en producción.

2. **Compatibilidad vehicular — MVP implementado.** Catálogo por marca, modelo, rango de años, motor, viscosidad, norma, capacidad y producto sugerido; consulta desde POS y cotizaciones. Los datos son capturados por el ERP y no se consideran una fuente certificada del fabricante; debe cargarse y revisarse un catálogo confiable.

3. **Evaluación de proveedores — MVP implementado.** Cada compra completamente recibida puede calificarse una sola vez en cumplimiento y puntualidad (1–5), con comentarios; hay detalle por recepción y promedio/resumen por proveedor. Falta acordar la escala interna y alimentar el historial operativo.

4. **Reparto — MVP implementado.** Se programa ruta, chofer, vehículo y placas desde una orden facturada y se liquida cada renglón como entregado o devuelto. No genera otra salida de inventario; toda devolución de una factura debe regularizarse por el flujo existente de nota de crédito para reponer stock y reflejar el efecto contable/fiscal.

5. **Tambores y retornables — MVP de control.** Se guarda el conteo físico cerrado/abierto por sucursal y producto, limitado a las existencias del ERP; movimientos de envases conservan saldo pendiente y depósito informativo por cliente. La clasificación de tambores debe reconciliarse tras las ventas y no se actualiza automáticamente con cada movimiento. Depósitos/devoluciones todavía no generan cobro, reembolso ni póliza contable; no deben usarse como saldo financiero oficial.

6. **Carta Porte 3.1 — captura y XML preliminar implementados.** Queda asociado al reparto y valida presencia/formato básico de RFC, permisos, vehículo, peso y códigos postales. El XML se etiqueta como borrador: no se valida contra XSD/catálogos SAT completos, no sustituye el CFDI de traslado/ingreso, no se sella y no se timbra ante PAC. **La certificación y el proceso fiscal completo siguen pendientes** antes de usarlo como documento de circulación.

**Cierre pendiente:** validar en una sucursal real los seis flujos con datos aprobados; cargar catálogo vehicular y precios autorizados; revisar el tratamiento contable de envases; comprobar devolución→nota de crédito→existencias; completar XSD/catálogos y timbrado de Carta Porte con PAC/SAT; documentar aceptación de usuarios.

### P2 — Mejoras acotadas

- [x] Mostrar saldo vencido, límite y crédito disponible en la búsqueda global de clientes y en el formulario de cotización. La cotización informa que no reserva crédito; la validación de venta existente sigue siendo la autoridad para aceptar o rechazar la operación.

- [x] Informar en la pestaña Estado de Resultados que las facturas históricas sin póliza no se incluyen ni se contabilizan retroactivamente.

- [x] Actualizar el `README.md` con el mapa de módulos y rutas actuales, corregir la referencia obsoleta al número de tablas y aclarar que el acceso depende de los permisos del usuario. Cambio documental; no modifica el porcentaje global aproximado por estar redondeado.

## Funciones que no cuento como pendientes

Contrasté código y documentación: **no incluiría de nuevo** límites de crédito, bloqueo por periodos cerrados, PDF obligatorio del CFDI por correo, las acciones principales de auditoría ni las tareas de hardcode/SOLID/DRY. Están implementadas según el código actual y las notas recientes `Deuda técnica al 15 de octubre` y `Deuda técnica SOLID/DRY`.

**Importante:** el `Checklist completitud ERP.md` contiene afirmaciones anteriores que ya no coinciden con el código. En particular, no usaría sus pendientes de crédito, PDF y deuda técnica sin volverlos a verificar. El archivo `Lo que falta para el 16 de octubre.md` ya tenía cambios locales y no lo modifiqué.

**Conclusión:** el ERP tiene un núcleo funcional amplio y pasa build y pruebas; lo que más falta para darlo por terminado es demostrar los flujos en operación real y decidir cuánto de las extensiones especializadas del giro se quiere incluir en la versión final.

## Avance de esta revisión

- [x] Actualización documental del `README.md` (17/10/2026): módulos actuales, rutas principales y puesta en marcha.
- [x] Contexto de crédito para clientes en búsqueda global y cotizaciones.
- [x] Aviso de alcance histórico en Estado de Resultados.
- Se implementaron las tres mejoras operativas priorizadas; no cambian las reglas de venta ni registran movimientos financieros.
- Los seis diferenciadores anteriores se implementaron como MVP, conservando los límites fiscales y contables indicados. El porcentaje de referencia queda en **~90 % completado / ~10 % pendiente**; es una estimación gruesa del alcance restante, no equivale a cobertura de pruebas ni porcentaje de código.

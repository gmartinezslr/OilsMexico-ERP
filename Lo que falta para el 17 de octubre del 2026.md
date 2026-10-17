Lo que falta para el 17 de octubre del 2026



revisa TODO el proyecto y dame un porcentaje total de lo que falta para terminar el ERP, porcentaje y tareas lo más detalladas posible. Evita lo que ya existe y está funcionando correctamente y no necesita ajustes, cambios o modificaciones



# Evaluación general

**Estimación total pendiente: ~14 %; avance estimado: ~86 %.** Es una estimación por alcance y esfuerzo, no un porcentaje de líneas de código.

Para el **núcleo operativo del ERP**, la implementación está aproximadamente en **94–96 %**. La diferencia hasta el 86 % total corresponde sobre todo a pruebas reales de operación y a funciones especializadas para el negocio de lubricantes que aún no existen.

La estimación **excluye** nómina/RR. HH., producción/MRP, multiempresa y API para terceros, que el proyecto identifica como fuera de alcance. Las funciones de precios por volumen, compatibilidad de aceites y reparto sí se cuentan como pendientes porque son diferenciadores del giro.

**Verificación realizada:** `dotnet build OilsMexico.slnx --no-restore` terminó con **0 errores y 0 advertencias**; `dotnet test OilsMexico.slnx --no-restore` pasó **52/52 pruebas**. Esto respalda el estado del código, pero no sustituye recorridos manuales en la UI ni pruebas con servicios externos.

## Trabajo pendiente estimado

| Prioridad | Área | Pendiente estimado |
| - | - | -: |
| P0 | Pruebas reales de Finkok y SMTP | ~3 % |
| P1 | Recorridos manuales de aceptación con UI y PostgreSQL | ~3 % |
| P1/P2 | Funciones comerciales y logísticas especializadas | ~6 % |
| P2 | Visibilidad del crédito, advertencia de reportes históricos y documentación | ~2 % |
| **Total estimado** |  | **~14 %** |


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

### P1/P2 — Diferenciadores aún no implementados

Estas son ampliaciones de alcance, no fallas de los módulos actuales:

1. **Listas de precios por tipo de cliente** y reglas de descuentos escalonados por volumen, integradas en POS y cotizaciones.

2. **Guía de compatibilidad de aceites por vehículo** —marca, modelo, año, motor y especificación— con consulta desde ventas y cotizaciones.

3. **Evaluación de proveedores** por cumplimiento y puntualidad, con reporte.

4. **Logística de reparto:** rutas, vehículos, choferes, remisiones y liquidación de entregas/devoluciones.

5. **Control de tambor abierto/granel** y envases retornables con depósitos o bonificaciones.

6. **Carta Porte 3.x:** servicio y flujo de usuario; los XSLT existentes por sí solos no completan esa función.

### P2 — Mejoras acotadas

- Mostrar más contexto de crédito —saldo vencido y disponibilidad— en búsqueda global y cotizaciones, si se desea ese aviso en esos puntos. **El límite, los días de crédito y los controles de venta ya existen** en `Cliente.cs`, `VentasService.Venta.cs` y `VentasPOS.razor`; no vuelvo a contarlos como implementación pendiente.

- Informar en la UI que facturas históricas sin póliza contable no aparecen en reportes basados en el libro.

- [x] Actualizar el `README.md` con el mapa de módulos y rutas actuales, corregir la referencia obsoleta al número de tablas y aclarar que el acceso depende de los permisos del usuario. Cambio documental; no modifica el porcentaje global aproximado por estar redondeado.

## Funciones que no cuento como pendientes

Contrasté código y documentación: **no incluiría de nuevo** límites de crédito, bloqueo por periodos cerrados, PDF obligatorio del CFDI por correo, las acciones principales de auditoría ni las tareas de hardcode/SOLID/DRY. Están implementadas según el código actual y las notas recientes `Deuda técnica al 15 de octubre` y `Deuda técnica SOLID/DRY`.

**Importante:** el `Checklist completitud ERP.md` contiene afirmaciones anteriores que ya no coinciden con el código. En particular, no usaría sus pendientes de crédito, PDF y deuda técnica sin volverlos a verificar. El archivo `Lo que falta para el 16 de octubre.md` ya tenía cambios locales y no lo modifiqué.

**Conclusión:** el ERP tiene un núcleo funcional amplio y pasa build y pruebas; lo que más falta para darlo por terminado es demostrar los flujos en operación real y decidir cuánto de las extensiones especializadas del giro se quiere incluir en la versión final.

## Avance de esta revisión

- [x] Actualización documental del `README.md` (17/10/2026): módulos actuales, rutas principales y puesta en marcha.
- El porcentaje de referencia permanece en **~86 % completado / ~14 % pendiente**; esta tarea de documentación ya queda excluida de los pendientes, sin alterar materialmente una estimación redondeada.

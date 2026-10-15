# Handoff — Clientes y Proveedores

## Evaluación de la propuesta

La propuesta original cubría los requisitos principales: listas de correos, teléfonos y direcciones; un respaldo para clientes; y el puesto/cargo de los contactos de proveedores. Sin embargo, necesitaba ajustes antes de usarse como plantilla:

- Gran parte del borrador estaba dentro de bloques de código y con caracteres Markdown escapados, por lo que no se mostraba como tablas listas para rellenar.
- No quedaba claro cómo asegurar que la persona de respaldo fuera distinta del contacto principal, tuviera medios de contacto y estuviera autorizada para recibir pedidos o notificaciones.
- Los datos de contacto aparecían repetidos en varias secciones, con riesgo de inconsistencias. Esta versión asigna un ID a cada contacto y lo usa para asociar correos, teléfonos y direcciones.
- Una única dirección predeterminada no es suficiente cuando una empresa tiene distintos domicilios fiscales y de entrega. Se identifica la dirección preferida por uso.
- Algunos campos comerciales solo aplican a clientes o proveedores; se separan para evitar valores como “N/A” en campos que no corresponden.

**Resultado:** la plantilla revisada cumple las reglas solicitadas y agrega criterios mínimos de captura y consistencia para facilitar el traspaso y su posterior carga en un sistema. Los ejemplos son ficticios y solo ilustrativos.

## Reglas de captura

1. Completar una fila por cada correo, teléfono, dirección o persona. Se permiten tantos registros como sean necesarios.
2. Identificar un contacto principal. Registrar los correos y teléfonos disponibles; cuando exista un medio preferido, marcar solo uno como principal por categoría. No inventar datos: indicar **Pendiente de confirmar** si hace falta.
3. Asociar cada medio de contacto con un ID de persona o indicar **Buzón/área** cuando sea compartido (por ejemplo, `Facturación`). No inventar una persona para un buzón genérico.
4. Para clientes, completar la sección **Persona de respaldo / Quien recibe** con alguien distinto del contacto principal, al menos un medio de contacto y el alcance de su autorización.
5. Para proveedores, no registrar ningún contacto sin su **Puesto / Cargo**. Esto aplica también al contacto principal.
6. Marcar direcciones predeterminadas por uso (fiscal, entrega u otro), no con una sola selección general. Si no se conoce un dato, indicar **Pendiente de confirmar**; no dejarlo ambiguo.
7. Usar números telefónicos con código de país cuando se conozca y registrar por separado el tipo de línea y si admite WhatsApp.

---

# Plantilla para completar

> **Tipo de tercero:** [CLIENTE / PROVEEDOR]  
> **Fecha de elaboración:** [DD/MM/AAAA]  
> **Elaborado por / área:** [Nombre — área]  
> **Versión:** [1.0]  
> **Estado:** [Borrador / En revisión / Aprobado / Vigente]

## 1. Datos generales

| Campo | Valor | Observaciones |
|---|---|---|
| Razón social / nombre | | |
| Nombre comercial | | |
| RFC / identificación fiscal | | |
| Régimen fiscal (si aplica) | | |
| Sitio web | | |
| Estatus de la relación | | |
| Notas generales | | |

## 2. Contactos

Agrega una fila por persona. Asigna IDs únicos (`C01`, `C02`...) y reutilízalos en las secciones 3–5.

| ID | Nombre completo | Puesto / cargo | Área / departamento | ¿Contacto principal? | Responsabilidad / notas |
|---|---|---|---|---|---|
| C01 | | | | [Sí / No] | |
| C02 | | | | [Sí / No] | |
| C03 | | | | [Sí / No] | |

## 3. Correos electrónicos

| ID | Tipo / uso | Dirección de correo | Contacto ID o buzón/área | ¿Correo principal? | Notas |
|---|---|---|---|---|---|
| E01 | Principal | | | [Sí / No] | |
| E02 | Compras / ventas | | | [Sí / No] | |
| E03 | Facturación | | | [Sí / No] | |
| E04 | Otro: | | | [Sí / No] | |

## 4. Teléfonos

| ID | Tipo de línea | Número (con código de país) | ¿Tiene WhatsApp? | Contacto ID o buzón/área | ¿Teléfono principal? | Horario / notas |
|---|---|---|---|---|---|---|
| T01 | Celular | | [Sí / No] | | [Sí / No] | |
| T02 | Oficina | | [Sí / No] | | [Sí / No] | |
| T03 | Otro: | | [Sí / No] | | [Sí / No] | |

## 5. Direcciones

| ID | Tipo / uso | Calle y número ext./int. | Colonia / localidad | Municipio / ciudad | Estado | CP | País | Contacto ID / recibe en sitio | ¿Predeterminada para este uso? | Horario / instrucciones |
|---|---|---|---|---|---|---|---|---|---|---|
| D01 | Fiscal | | | | | | | | [Sí / No] | |
| D02 | Entrega | | | | | | | | [Sí / No] | |
| D03 | Sucursal / otra | | | | | | | | [Sí / No] | |

> Usos sugeridos: fiscal, entrega, pagos, devoluciones y sucursal. Si hay varias direcciones del mismo uso, indica cuál es la predeterminada para ese uso.

## 6. Datos específicos de CLIENTES

### Persona de respaldo / Quien recibe — obligatorio

Debe ser distinta del contacto principal. Sus datos de contacto se capturan también en las secciones 2–4; usa aquí el mismo ID para evitar duplicados.

| Campo | Valor |
|---|---|
| Contacto ID y nombre | |
| ¿Puede recibir pedidos? | [Sí / No] |
| ¿Puede recibir notificaciones? | [Sí / No] |
| Alcance de autorización (por ejemplo, recibir mercancía; aclarar si puede firmar) | |
| Horario / disponibilidad | |
| Medio preferido para escalamientos | |
| Indicaciones cuando el contacto principal no esté disponible | |

### Condiciones comerciales y operativas

| Campo | Valor | Observaciones |
|---|---|---|
| Ejecutivo asignado (nuestro) | | |
| Condiciones / días de crédito | | |
| Límite de crédito y moneda | | |
| Lista de precios aplicable | | |
| Dirección predeterminada de entrega (ID) | | |
| Datos o sucursal de facturación | | |
| Ventanas / restricciones de entrega | | |
| Otras observaciones operativas | | |

## 7. Datos específicos de PROVEEDORES

Verifica que cada persona de la sección 2 —incluido el contacto principal— tenga **Puesto / cargo**. Si la empresa proveedora es una persona física, registra su nombre y un cargo descriptivo como “Propietario” o “Proveedor independiente”.

### Condiciones comerciales y operativas

| Campo | Valor | Observaciones |
|---|---|---|
| Comprador / responsable asignado (nuestro) | | |
| Condiciones y método de pago | | |
| Moneda / datos de pago verificados por | | No incluir números completos de cuenta o credenciales en este handoff; usar el canal seguro aprobado. |
| Lista de precios / vigencia | | |
| Pedido mínimo | | |
| Tiempo de entrega estimado | | |
| Dirección predeterminada de recepción / recolección (ID) | | |
| Horario / restricciones de carga o recepción | | |
| Otras observaciones operativas | | |

## 8. Confirmación y seguimiento

| Campo | Valor |
|---|---|
| Información confirmada con (nombre / área) | |
| Fecha de última confirmación | |
| Próxima revisión recomendada | |
| Responsable interno de mantenerla actualizada | |

| Versión | Fecha | Cambio realizado | Autor |
|---|---|---|---|
| 1.0 | | Emisión inicial | |

---

# Ejemplo ficticio — Cliente

> **Tipo de tercero:** CLIENTE  
> **Fecha de elaboración:** 15/10/2026  
> **Elaborado por / área:** Ana López — Cuentas por cobrar  
> **Versión:** 1.0  
> **Estado:** Ejemplo ilustrativo

## 1. Datos generales

| Campo | Valor | Observaciones |
|---|---|---|
| Razón social / nombre | Comercial Ejemplo del Norte, S.A. de C.V. | Nombre ficticio |
| Nombre comercial | Ferretería Ejemplo | |
| RFC / identificación fiscal | AAA010101AA0 | Dato ficticio de ejemplo; confirmar RFC real antes de usar |
| Régimen fiscal (si aplica) | Pendiente de confirmar | |
| Sitio web | https://cliente.example.com | Dominio reservado para ejemplos |
| Estatus de la relación | Activo | |
| Notas generales | Cliente mayorista; recibe mercancía en su almacén principal. | |

## 2. Contactos

| ID | Nombre completo | Puesto / cargo | Área / departamento | ¿Contacto principal? | Responsabilidad / notas |
|---|---|---|---|---|---|
| C01 | Alex Ejemplo | Gerente de compras | Compras | Sí | Contacto principal para pedidos |
| C02 | Sam Ejemplo | Encargado de almacén | Almacén | No | Respaldo y recepción de mercancía |
| C03 | Pat Ejemplo | Analista de cuentas por pagar | Finanzas | No | Consultas de facturación y pagos |

## 3. Correos electrónicos

| ID | Tipo / uso | Dirección de correo | Contacto ID o buzón/área | ¿Correo principal? | Notas |
|---|---|---|---|---|---|
| E01 | Principal | compras@cliente.example.com | C01 | Sí | Pedidos y coordinación comercial |
| E02 | Facturación | facturas@cliente.example.com | C03 | No | Recepción de CFDI |
| E03 | Entregas | almacen@cliente.example.com | C02 | No | Confirmar llegada de pedidos |

## 4. Teléfonos

| ID | Tipo de línea | Número (con código de país) | ¿Tiene WhatsApp? | Contacto ID o buzón/área | ¿Teléfono principal? | Horario / notas |
|---|---|---|---|---|---|---|
| T01 | Celular | +52 55 0000 0101 | Sí | C01 | Sí | L–V, horario laboral |
| T02 | Oficina | +52 55 0000 0102 | No | C02 | No | Extensión de almacén |
| T03 | Oficina | +52 55 0000 0103 | No | C03 | No | Consultas de pagos |

## 5. Direcciones

| ID | Tipo / uso | Calle y número ext./int. | Colonia / localidad | Municipio / ciudad | Estado | CP | País | Contacto ID / recibe en sitio | ¿Predeterminada para este uso? | Horario / instrucciones |
|---|---|---|---|---|---|---|---|---|---|---|
| D01 | Fiscal | Av. Ejemplo 100 | Centro | Ciudad Ejemplo | Estado Ejemplo | 00000 | México | C03 | Sí | Uso fiscal |
| D02 | Entrega | Calle de la Muestra 25, Bodega 2 | Zona Industrial | Ciudad Ejemplo | Estado Ejemplo | 00001 | México | C02 | Sí | L–V, 08:00–15:00; avisar antes de llegar |

## 6. Datos específicos de CLIENTES

### Persona de respaldo / Quien recibe — obligatorio

| Campo | Valor |
|---|---|
| Contacto ID y nombre | C02 — Sam Ejemplo |
| ¿Puede recibir pedidos? | Sí |
| ¿Puede recibir notificaciones? | Sí |
| Alcance de autorización | Puede recibir y firmar remisiones; no puede modificar pedidos ni autorizar cargos adicionales. |
| Horario / disponibilidad | L–V, 08:00–15:00 |
| Medio preferido para escalamientos | Teléfono T02; correo E03 |
| Indicaciones cuando el contacto principal no esté disponible | Contactar a C02 antes del despacho. |

### Condiciones comerciales y operativas

| Campo | Valor | Observaciones |
|---|---|---|
| Ejecutivo asignado (nuestro) | Taylor Ejemplo — Ventas | |
| Condiciones / días de crédito | 30 días | Sujeto a aprobación vigente |
| Límite de crédito y moneda | $100,000 MXN | Ejemplo ficticio |
| Lista de precios aplicable | Mayoreo — ejemplo | |
| Dirección predeterminada de entrega (ID) | D02 | |
| Datos o sucursal de facturación | Sucursal de ejemplo | Confirmar razón social y uso CFDI por el canal habitual |
| Ventanas / restricciones de entrega | L–V, 08:00–15:00 | Requiere cita |
| Otras observaciones operativas | No dejar mercancía sin recepción confirmada. | |

## 8. Confirmación y seguimiento

| Campo | Valor |
|---|---|
| Información confirmada con (nombre / área) | Alex Ejemplo — Compras |
| Fecha de última confirmación | 15/10/2026 — ejemplo |
| Próxima revisión recomendada | 15/01/2027 — ejemplo |
| Responsable interno de mantenerla actualizada | Taylor Ejemplo — Ventas |

| Versión | Fecha | Cambio realizado | Autor |
|---|---|---|---|
| 1.0 | 15/10/2026 | Emisión inicial — ejemplo | Ana López |

---

# Ejemplo ficticio — Proveedor

> **Tipo de tercero:** PROVEEDOR  
> **Fecha de elaboración:** 15/10/2026  
> **Elaborado por / área:** Luis Ejemplo — Compras  
> **Versión:** 1.0  
> **Estado:** Ejemplo ilustrativo

## 1. Datos generales

| Campo | Valor | Observaciones |
|---|---|---|
| Razón social / nombre | Suministros de Muestra, S.A. de C.V. | Nombre ficticio |
| Nombre comercial | Suministros Muestra | |
| RFC / identificación fiscal | BBB010101BB1 | Dato ficticio de ejemplo; confirmar RFC real antes de usar |
| Régimen fiscal (si aplica) | Pendiente de confirmar | |
| Sitio web | https://proveedor.example.com | Dominio reservado para ejemplos |
| Estatus de la relación | Activo | |
| Notas generales | Suministro de lubricantes; embarques desde planta de ejemplo. | |

## 2. Contactos

| ID | Nombre completo | Puesto / cargo | Área / departamento | ¿Contacto principal? | Responsabilidad / notas |
|---|---|---|---|---|---|
| C01 | Robin Ejemplo | Ejecutivo de cuenta | Ventas | Sí | Cotizaciones y órdenes |
| C02 | Casey Ejemplo | Coordinadora de logística | Logística | No | Programación y seguimiento de embarques |
| C03 | Jamie Ejemplo | Encargado de facturación | Finanzas | No | CFDI y aclaraciones de pago |

## 3. Correos electrónicos

| ID | Tipo / uso | Dirección de correo | Contacto ID o buzón/área | ¿Correo principal? | Notas |
|---|---|---|---|---|---|
| E01 | Principal | ventas@proveedor.example.com | C01 | Sí | Cotizaciones y pedidos |
| E02 | Logística | embarques@proveedor.example.com | C02 | No | Programación de entrega |
| E03 | Facturación | facturacion@proveedor.example.com | C03 | No | Envío y corrección de CFDI |

## 4. Teléfonos

| ID | Tipo de línea | Número (con código de país) | ¿Tiene WhatsApp? | Contacto ID o buzón/área | ¿Teléfono principal? | Horario / notas |
|---|---|---|---|---|---|---|
| T01 | Celular | +52 55 0000 0201 | Sí | C01 | Sí | L–V, horario laboral |
| T02 | Oficina | +52 55 0000 0202 | No | C02 | No | Extensión de embarques |
| T03 | Oficina | +52 55 0000 0203 | No | C03 | No | Extensión de facturación |

## 5. Direcciones

| ID | Tipo / uso | Calle y número ext./int. | Colonia / localidad | Municipio / ciudad | Estado | CP | País | Contacto ID / recibe en sitio | ¿Predeterminada para este uso? | Horario / instrucciones |
|---|---|---|---|---|---|---|---|---|---|---|
| D01 | Fiscal | Av. Industrial 200 | Parque Ejemplo | Ciudad Ejemplo | Estado Ejemplo | 00010 | México | C03 | Sí | Uso fiscal |
| D02 | Entrega / recolección | Camino de Muestra 50 | Parque Logístico | Ciudad Ejemplo | Estado Ejemplo | 00011 | México | C02 | Sí | L–V, 07:00–16:00; acceso con cita |

## 7. Datos específicos de PROVEEDORES

Cada contacto tiene puesto/cargo registrado en la sección 2.

### Condiciones comerciales y operativas

| Campo | Valor | Observaciones |
|---|---|---|
| Comprador / responsable asignado (nuestro) | Luis Ejemplo — Compras | |
| Condiciones y método de pago | 30 días; transferencia | Ejemplo; validar condiciones vigentes |
| Moneda / datos de pago verificados por | MXN; verificación pendiente | Validar cambios de cuenta mediante el procedimiento seguro; no almacenar datos bancarios completos aquí |
| Lista de precios / vigencia | Lista de muestra — 2026 | Confirmar vigencia antes de ordenar |
| Pedido mínimo | 200 litros | Ejemplo ficticio |
| Tiempo de entrega estimado | 3–5 días hábiles | Desde confirmación de orden |
| Dirección predeterminada de recepción / recolección (ID) | D02 | |
| Horario / restricciones de carga o recepción | L–V, 07:00–16:00 | Requiere cita y equipo de protección |
| Otras observaciones operativas | Solicitar número de guía al confirmar despacho. | |

## 8. Confirmación y seguimiento

| Campo | Valor |
|---|---|
| Información confirmada con (nombre / área) | Robin Ejemplo — Ventas |
| Fecha de última confirmación | 15/10/2026 — ejemplo |
| Próxima revisión recomendada | 15/01/2027 — ejemplo |
| Responsable interno de mantenerla actualizada | Luis Ejemplo — Compras |

| Versión | Fecha | Cambio realizado | Autor |
|---|---|---|---|
| 1.0 | 15/10/2026 | Emisión inicial — ejemplo | Luis Ejemplo |

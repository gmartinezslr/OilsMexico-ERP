~~**DOCUMENTO DE HANDOFF / ESPECIFICACIONES TÉCNICAS**

~~**Módulo: Proyección de Ventas, Control de Cuotas y Pago de Comisiones  
Giro del Negocio: Distribución y Venta de Aceites y Lubricantes Automotrices  
Estatus del Core: Las comisiones se detonan estrictamente bajo Cobranza Efectiva (Cash Basis).**


~~**1. OBJETIVO DEL SISTEMA**

~~**Desarrollar o integrar un módulo en el ERP que permita proyectar metas de venta (en dinero y volumen), realizar el cruce automático contra las facturas cobradas en el mes y calcular las comisiones de los vendedores basándose en un tabulador dinámico por tipo de envase/producto. El sistema debe soportar penalizaciones por incumplimiento y reglas automáticas para nuevos productos o clientes.**


~~**2. ARQUITECTURA DE LA BASE DE DATOS (MER)**

~~**Cualquier IA que implemente este sistema debe crear o adaptar las siguientes tablas con sus respectivas relaciones:**

~~**Tabla: `cat\_presentaciones` (Configuración de Productos y Litros Equivalentes)**

~~**Almacena los factores de conversión a litros y los porcentajes de comisión por tipo de envase.**

- ~~**`id\_presentacion` (INT, PK, AI)**

- ~~**`descripcion` (VARCHAR) -\> Ej. 'Envase 0.946L', 'Garrafa 4.73L', 'Tambor 208L'**

- ~~**`litros\_equivalentes` (DECIMAL(10,3)) -\> Factor de conversión (0.946, 4.733, 208.000)**

- ~~**`porc\_comision\_base` (DECIMAL(5,2)) -\> % si NO cumple la meta (si aplica pago parcial)**

- ~~**`porc\_comision\_bono` (DECIMAL(5,2)) -\> % si SÍ cumple la meta máxima**

~~**Tabla: `metas\_vendedor` (Configuración Mensual de Cuotas)**

~~**Almacena los objetivos asignados a cada vendedor por periodo.**

- ~~**`id\_meta` (INT, PK, AI)**

- ~~**`id\_vendedor` (INT, FK -\> `usuarios`)**

- ~~**`periodo\_mes\_ano` (VARCHAR(7)) -\> Ej. '2026-10'**

- ~~**`meta\_dinero` (DECIMAL(12,2), NULLABLE) -\> Objetivo en monto monetario ($)**

- ~~**`meta\_volumen\_litros` (DECIMAL(12,2), NULLABLE) -\> Objetivo en Litros Equivalentes totales**

- ~~**`operador\_logico` (ENUM('SOLO\_DINERO', 'SOLO\_VOLUMEN', 'O', 'Y')) -\> Condición de liberación**

- ~~**`accion\_incumplimiento` (ENUM('CERO\_COMISION', 'PAGO\_MINIMO')) -\> Comportamiento si falla la meta**

- ~~**`monto\_pago\_minimo` (DECIMAL(12,2)) -\> Monto fijo a pagar si aplica 'PAGO\_MINIMO'**

~~**Tabla: `comisiones\_historico` (Cierre y Congelación de Datos)**

~~**Almacena el registro histórico auditables de los pagos mensuales calculados.**

- ~~**`id\_historico` (INT, PK, AI)**

- ~~**`id\_vendedor` (INT, FK)**

- ~~**`periodo\_mes\_ano` (VARCHAR(7))**

- ~~**`ventas\_dinero\_real` (DECIMAL(12,2)) -\> Lo cobrado en el mes**

- ~~**`litros\_reales\_desplazados` (DECIMAL(12,2)) -\> Los litros cobrados en el mes**

- ~~**`cumplio\_meta` (BOOLEAN) -\> Resultado de la evaluación lógica**

- ~~**`monto\_final\_pagado` (DECIMAL(12,2)) -\> Output del algoritmo de cálculo**

- ~~**`fecha\_calculo` (DATETIME)**


~~**3. LÓGICA DEL MOTOR DE CÁLCULO (PSEUDOCÓDIGO)**

~~**El script de cierre mensual (o consulta en tiempo real) debe ejecutar la siguiente secuencia lógica:**

~~**python**

```
~~**`\# 1. OBTENER CONFIGURACIÓN DE LA META DEL VENDEDOR`**

~~**`meta = DB.query("SELECT \* FROM metas\_vendedor WHERE id\_vendedor = ? AND periodo\_mes\_ano = ?", vendedor\_id, periodo\_actual)`**


~~**`\# 2. CALCULAR EL DESEMPEÑO REAL ACUMULADO DEL MES (Basado estrictamente en pagos recibidos)`**

~~**`\# Se extraen los productos de las facturas afectadas por los recibos de pago del mes`**

~~**`pagos\_del\_mes = DB.query("""`**

~~`    **SELECT fd.cantidad, cp.litros\_equivalentes, fd.precio\_neto\_cobrado, cp.id\_presentacion`**

~~`    **FROM recibos\_pago rp`**

~~`    **JOIN facturas f ON rp.id\_factura = f.id\_factura`**

~~`    **JOIN facturas\_detalle fd ON f.id\_factura = fd.id\_factura`**

~~`    **JOIN productos p ON fd.id\_producto = p.id\_producto`**

~~`    **JOIN cat\_presentaciones cp ON p.id\_presentacion = cp.id\_presentacion`**

~~`    **WHERE f.id\_vendedor = ? AND rp.fecha\_pago BETWEEN ? AND ?`**

~~**`""", vendedor\_id, fecha\_inicio\_mes, fecha\_fin\_mes)`**


~~**`totales\_dinero\_real = 0`**

~~**`totales\_litros\_real = 0`**


~~**`for item in pagos\_del\_mes:`**

~~`    **totales\_dinero\_real += item.precio\_neto\_cobrado`**

~~`    **totales\_litros\_real += (item.cantidad \* item.litros\_equivalentes)`**


~~**`\# 3. EVALUACIÓN DE LA META (OPERADORES LÓGICOS)`**

~~**`meta\_dinero\_ok = totales\_dinero\_real \>= meta.meta\_dinero if meta.meta\_dinero else False`**

~~**`meta\_volumen\_ok = totales\_litros\_real \>= meta.meta\_volumen\_litros if meta.meta\_volumen\_litros else False`**


~~**`cumplio\_condicion = False`**

~~**`if meta.operador\_logico == 'SOLO\_DINERO':`**

~~`    **cumplio\_condicion = meta\_dinero\_ok`**

~~**`elif meta.operador\_logico == 'SOLO\_VOLUMEN':`**

~~`    **cumplio\_condicion = meta\_volumen\_ok`**

~~**`elif meta.operador\_logico == 'O':`**

~~`    **cumplio\_condicion = (meta\_dinero\_ok or meta\_volumen\_ok)`**

~~**`elif meta.operador\_logico == 'Y':`**

~~`    **cumplio\_condicion = (meta\_dinero\_ok and meta\_volumen\_ok)`**


~~**`\# 4. APLICACIÓN DE LAS REGLAS DE PAGO`**

~~**`monto\_comision\_total = 0`**


~~**`if cumplio\_condicion:`**

~~`    **\# Si cumple la meta, se calcula partida por partida usando el porcentaje de BONO`**

~~`    **for item in pagos\_del\_mes:`**

~~`        **porcentaje = DB.query("SELECT porc\_comision\_bono FROM cat\_presentaciones WHERE id\_presentacion = ?", item.id\_presentacion)`**

~~`        **monto\_comision\_total += item.precio\_neto\_cobrado \* (porcentaje / 100)`**

~~**`else:`**

~~`    **\# Si NO cumple la meta, evaluar penalización`**

~~`    **if meta.accion\_incumplimiento == 'CERO\_COMISION':`**

~~`        **monto\_comision\_total = 0.00`**

~~`    **elif meta.accion\_incumplimiento == 'PAGO\_MINIMO':`**

~~`        **monto\_comision\_total = meta.monto\_pago\_minimo`**


~~**`\# 5. GUARDAR O DESPLEGAR RESULTADO`**

~~**`CerrarPeriodoYGuardar(vendedor\_id, periodo\_actual, totales\_dinero\_real, totales\_litros\_real, cumplio\_condicion, monto\_comision\_total)`**
```

~~**Usa el código con precaución.**


~~**4. REGLAS DE NEGOCIO CRÍTICAS E INTEGRACIÓN AUTOMÁTICA**

- ~~**Cash Basis Estricto: Ninguna factura en estado "Pendiente" o "Vencida" entra en el pool de cálculo. El disparador de datos son los Recibos de Pago aplicados dentro del mes calendario. En caso de pagos parciales, la base del cálculo es la proporción neta del dinero ingresado a banco (restando impuestos).**

- ~~**Herencia Automática para Nuevos Productos: Todo SKU nuevo que se registre en la tabla `productos` debe contar obligatoriamente con una llave foránea hacia `cat\_presentaciones`. Con esto, el ERP sumará sus litros equivalentes y aplicará su porcentaje asignado automáticamente sin requerir actualizaciones de código.**

- ~~**Herencia Automática para Nuevos Clientes: Al dar de alta un cliente, el ERP exige la asignación de un `id\_vendedor`. Toda la cobranza generada por este cliente sumará directamente a las métricas del vendedor dueño de la cuenta.**

- ~~**Notas de Crédito y Devoluciones: Si se aplica una nota de crédito por devolución de producto en el periodo actual, el ERP debe restar los Litros Equivalentes y el Monto Monetario correspondientes del acumulado del mes del vendedor para evitar pagos sobre ventas canceladas.**


~~**5. UI/UX Y ESPECIFICACIONES DE PANTALLAS**

~~**Pantalla A: Panel de Control de Metas (Administrador)**

- ~~**Formulario de asignación por Vendedor y Mes.**

- ~~**Checkboxes independientes para activar `Meta de Dinero ($)` y `Meta de Volumen (Litros)`.**

- ~~**Dropdown para seleccionar la regla lógica de liberación (`Y`, `O`, `Solo Dinero`, `Solo Volumen`).**

- ~~**Dropdown selector para la acción en caso de no llegar a la meta (`Cero Comisión` o `Pago Mínimo Garantizado` con campo numérico de monto).**

~~**Pantalla B: Portal del Vendedor (Dashboard de Avance)**

- ~~**Barra de progreso visual de cumplimiento de meta de dinero y volumen en tiempo real.**

- ~~**Indicador numérico de "Run Rate" (Tendencia predictiva basada en los días transcurridos del mes).**

- ~~**Listado detallado de facturas cobradas en el mes que están sumando a su comisión actual.**

- ~~**Sección de "Comisiones Potenciales": Listado de facturas emitidas pendientes de pago, mostrando la comisión que el vendedor liberaría si logra cobrar esa factura antes de que termine el mes.**


~~**Instrucciones para la IA Implementadora:**

1. ~~**Diseña las migraciones de base de datos respetando los tipos de datos decimales para evitar pérdida de centavos en montos o mililitros en volúmenes.**

2. ~~**Crea los triggers o listeners necesarios en la tabla de pagos/recibos para actualizar los acumulados dinámicamente.**

3. ~~**Asegúrate de congelar los registros en `comisiones\_historico` al ejecutar el cierre para evitar alteraciones retroactivas si cambian las metas en el futuro.**


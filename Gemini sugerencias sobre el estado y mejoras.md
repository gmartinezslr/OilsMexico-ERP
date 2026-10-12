Esta aplicación está desarrollada con .NET 10, C\# y blazor, la base de datos es PostgreSQL, está orientada para un distribuidor de aceites automotrices. El estado actual se desglosa en el archivo: "Situación al 11 de octubre.md"


1. revisa que mejoras se pueden hacer al código ya existente y que cumpla con los principios SOLID y no haya hardcode.

2. revisa que funcionalidades nuevas o no existentes faltan para completar este ERP.



A continuación presento una auditoría exhaustiva del estado actual de **OilsMexico ERP**, dividida en:

1. **Mejoras al código existente (Principios SOLID y erradicación de Hardcode)** con citas exactas de archivos y líneas.

2. **Funcionalidades faltantes para completar el ERP**, tanto a nivel general como en las particularidades críticas de un **distribuidor de aceites automotrices en México**.


## 1. Mejoras al código existente: Principios SOLID y Hardcode

En los commits recientes (`cad99a1`, `868948c`, `050b76a`) se corrigieron aspectos clave como la unificación del IVA en [Impuestos.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Domain/Services/Impuestos.cs), la creación de [Roles.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Domain/Services/Roles.cs), las reglas de asientos en BD y la validación de RFC. Sin embargo, persisten violaciones importantes que afectan el mantenimiento y la arquitectura limpia:

### A. Cumplimiento de Principios SOLID

```
                             ARQUITECTURA ACTUAL vs SOLID  
┌───────────────────────────────┐                  ┌───────────────────────────────┐  
│        SITUACIÓN ACTUAL       │                  │       MEJORA PROPUESTA        │  
├───────────────────────────────┤                  ├───────────────────────────────┤  
│ • Páginas Blazor inyectan     │  DIP Violation   │ • UI inyecta solo interfaces  │  
│   ErpDbContext directamente   │ ───────────────\> │   (IProductosService, etc.)   │  
│ • AuthService (28 KB): login, │  SRP Violation   │ • Separar en IAuditService,   │  
│   2FA, QR, RFC, Passwords     │ ───────────────\> │   ITwoFactorService, etc.     │  
│ • NavMenu y Home: docenas     │  OCP Violation   │ • Menú y Dashboard basados en │  
│   de if(Rol is Roles.X)       │ ───────────────\> │   permisos dinámicos en BD    │  
│ • IVentasService monolítico   │  ISP Violation   │ • Dividir en IVentasPosService│  
│   (POS + Surtir + Historial)  │ ───────────────\> │   e IHistorialVentasService   │  
└───────────────────────────────┘                  └───────────────────────────────┘
```


#### 1. Inversión de Dependencias (DIP) — *Prioridad Alta*

- **Problema:** Múltiples componentes Blazor inyectan directamente `ErpDbContext Db`:

  - [Productos.razor:3](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Productos.razor#L3) y [Productos.razor.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Productos.razor.cs#L48-L66)

  - [Compras.razor:7](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Compras.razor#L7)

  - [Usuarios.razor:3](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Usuarios.razor#L3)

  - [VentasPOS.razor.cs:20](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/VentasPOS.razor.cs#L20)

  - [Metas.razor.cs:24](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Metas.razor.cs#L24)

  - [Home.razor:3](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/Home.razor#L3)

  - [Clientes.razor:3](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Clientes.razor#L3), [Proveedores.razor:3](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Proveedores.razor#L3), [Almacen.razor:7](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Almacen.razor#L7)

- **Riesgo:** La capa de presentación está acoplada directamente a Entity Framework Core. Además, en Blazor Server compartir el contexto de base de datos dentro del circuito provoca errores de concurrencia: `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`.

- **Mejora:** La UI debe comunicarse exclusivamente mediante contratos de aplicación (`IProductosService`, `IUsuariosService`, etc.). Toda consulta LINQ debe residir en la capa de servicios.

#### 2. Responsabilidad Única (SRP) — *Prioridad Alta*

- **[AuthService.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/AuthService.cs):** Esta clase contiene más de 500 líneas y asume 7 responsabilidades distintas:

  1. Login, hashing y actualización progresiva de contraseñas.

  2. Política de bloqueo por intentos fallidos.

  3. Generación y verificación de TOTP / 2FA.

  4. Generación gráfica de códigos QR con `System.Drawing` ([AuthService.cs:271-276](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/AuthService.cs#L271-L276)).

  5. Configuración y vigencia de contraseñas.

  6. Registro de bitácoras de auditoría (`AuditLog`).

  7. **Validación de RFC fiscal del SAT** ([AuthService.cs:290-299](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/AuthService.cs#L290-L299)), lo cual es dominio fiscal/tributario y no tiene ninguna relación con autenticación de usuarios.

  8. *Mejora:* Extraer en `IPasswordPolicyService`, `ITwoFactorService`, `IAuditService`, y mover la validación de RFC a un servicio fiscal (`IFiscalService` / `IRfcValidadorService`).

- **[CfdiServices.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/CfdiServices.cs):** Monolito de 50 KB y más de 920 líneas que concentra:

  1. Construcción de XML para Facturas (I), Notas de Crédito (E) y Recibos Electrónicos de Pago (P - Pagos 2.0).

  2. Algoritmos criptográficos CSD (PKCS\#8 y descifrado de llave `.key`).

  3. Transformación XSLT de la Cadena Original SAT.

  4. Cliente SOAP y comunicación de red contra el PAC Finkok.

  5. Lógica de reintentos por incidencia SAT 307 y cancelación con firma digital.

  6. *Mejora:* Dividir en `ICsdCryptoService`, `ICadenaOriginalService`, `ICfdiXmlBuilder` e `IPacClient`.

- **Lógica de negocio dentro de la UI en [Productos.razor.cs:143-156](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Productos.razor.cs#L143-L156):** El alta de productos en el code-behind crea manualmente los registros de `InventarioSucursal` para todas las sucursales y la `UnidadMedida` base por defecto. Esa regla de negocio debe vivir en un caso de uso (`CrearProductoAsync`) para que si un producto se importa o se crea por API, no quede huérfano de inventario.

#### 3. Abierto/Cerrado (OCP) — *Prioridad Media*

- **[NavMenu.razor](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Layout/NavMenu.razor):** Contiene 376 líneas donde casi cada grupo y enlace evalúa bloques `@if (Sesion.Sesion?.Rol is Roles.Admin or Roles.Vendedor...)`. Si el negocio requiere crear un nuevo rol (e.g. `GerenteSucursal`, `Facturista`, `Repartidor`, `Auditor`), es necesario modificar docenas de directivas en el archivo Razor.

  - *Mejora:* Aprovechar las entidades existentes [Role.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Domain/Entities/Role.cs), `Permiso` y `RolPermiso` para evaluar permisos dinámicos: `Sesion.TienePermiso("VerPOS")` o un diccionario de menús por rol.

- **[Home.razor:304-331](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/Home.razor#L304-L331):** La construcción de los KPIs del tablero principal utiliza una cascada `if/else if` sobre el rol (`rol is Roles.Vendedor or Roles.Admin`, `else if (rol == Roles.Almacen)`, `else if (rol == Roles.Conta)`). Debe utilizarse el patrón Estrategia o un servicio `IDashboardService.ObtenerKpisAsync(usuario)`.

- **Integración con PAC en [CfdiServices.cs:716-725](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/CfdiServices.cs#L716-L725):** Está acoplada de forma fija a los endpoints y namespaces SOAP de Finkok (`xmlns:ca="http://facturacion.finkok.com/cancel"`). Cambiar de proveedor (ej. SW Sapience, Solución Factible o Edicom) obligaría a reescribir la lógica de timbrado.

#### 4. Segregación de Interfaces (ISP) — *Prioridad Media*

- **[IVentasService](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Application/Interfaces/Servicios.cs#L40-L53):** Mezcla el registro de venta POS en mostrador (`RegistrarVentaAsync`), el despacho de almacén (`SurtirAsync`), la devolución de producto (`DevolverAsync`) y la consulta paginada de auditoría (`HistorialAsync`, `HistorialDetalleAsync`).

  - *Mejora:* Separar en `IVentasPosService`, `IEntregaAlmacenService` e `IHistorialVentasService`. Un componente que solo lee historial no debe conocer métodos de cobro o cancelación.

- **[IAsientoGeneradorService](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Application/Interfaces/Servicios.cs):** Los métodos `GenerarAsientosDeCompraAsync` y `GenerarAsientosDeDevolucionAsync` solicitan los parámetros `sucursalId` y `usuarioId` que en la implementación [AsientoGeneradorService.cs:48-56](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/AsientoGeneradorService.cs#L48-L56) son completamente ignorados.


### B. Hardcode Remanente a Mover a Tablas o Constantes

| Ubicación | Hardcode Detectado | Solución Recomendada |
| - | - | - |
| [Compras.razor:250-255](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Compras.razor#L250-L255) | Opciones de forma de pago en HTML: `\<option value="01"\>Efectivo\</option\>`, `02`, `03`, `04`, `28`, `99`. | Consumir el catálogo SAT inyectando `ICatalogosSatService` (o desde tabla `catalogos\_sat`). |
| [NotasCredito.razor:112-115](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/NotasCredito.razor#L112-L115) y [Facturacion.razor:151](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Facturacion.razor#L151) | Motivos de cancelación SAT repetidos a mano en HTML (`01`, `02`, `03`, `04`). | Centralizar en `CfdiDefaults.cs` o exponerlos vía `ICatalogosSatService`. |
| [Usuarios.razor:89-92](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Usuarios.razor#L89-L92) | Opciones de roles fijas: `\<option value="Admin"\>...`, `Vendedor`, `Almacen`, `Conta`. | Iterar sobre `Roles.Todos` o leer de la tabla `Roles` en BD. |
| [Productos.razor.cs:74-78, 149-153](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Productos.razor.cs#L74-L78) | Valores fijos de negocio: `TipoBase = "Sintetico"`, `Categoria = "SINTETICOS"`, `StockMinimo = 5`, `FactorConversion = 1m`, `"Litro"`. | Extraer a la configuración general o catálogo de unidades base. |
| [Home.razor:308, 325](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/Home.razor#L308) | Cadenas informativas fijas: `"IVA 16% incluido"` y `"0", "Módulo futuro (PAC)"`. | Calcular dinámicamente según `Impuestos.TasaIva \* 100` y consultar datos reales del servicio PAC. |
| [AsientoGeneradorService.cs:34, 79](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/AsientoGeneradorService.cs#L34) | Consulta N+1: `await db.CuentasContables.FindAsync(\[r.CuentaId\])` dentro de un bucle `foreach`. | Cargar las cuentas mediante un `.Include()` o consulta agrupada `.Where(...)`. |



## 2. Funcionalidades faltantes para completar el ERP

Un ERP para un **distribuidor de aceites automotrices y lubricantes** no es un comercio genérico; maneja dinámicas comerciales específicas: clientes mecánicos y refaccionarias que operan a crédito, entrega de tambores de 208 litros vs. garrafas de 4 litros, ventas fraccionadas a granel, y rutas de reparto.

A continuación se detalla la matriz de requerimientos faltantes:

```
                            MAPA DE FALTANTES DEL ERP  
┌─────────────────────────────────┐           ┌─────────────────────────────────┐  
│     COMERCIAL Y VENTAS          │           │       LOGÍSTICA Y ALMACÉN       │  
├─────────────────────────────────┤           ├─────────────────────────────────┤  
│ • Cotizaciones y Órdenes Venta  │           │ • Despacho granel/tambor abierto│  
│ • Listas de precios por cliente │           │ • Rutas de reparto y choferes   │  
│ • Buscador de compatibilidad    │           │ • Control envases y tambores    │  
│ • Control de crédito y mora     │           │ • Carta Porte 3.0 para traslados│  
└─────────────────────────────────┘           └─────────────────────────────────┘  
                 │                                             │  
                 ▼                                             ▼  
┌─────────────────────────────────┐           ┌─────────────────────────────────┐  
│      FINANZAS Y TESORERÍA       │           │     OPERACIÓN Y CUMPLIMIENTO    │  
├─────────────────────────────────┤           ├─────────────────────────────────┤  
│ • Gastos operativos (/gastos)   │           │ • Exportación Excel / PDF       │  
│ • Conciliación bancaria         │           │ • Envío automático CFDI (mail)  │  
│ • Costo de ventas FIFO real     │           │ • Matriz RBAC granular         │  
│ • Cierre contable y balances    │           │ • Auditoría de transacciones    │  
└─────────────────────────────────┘           └─────────────────────────────────┘
```

### 1. Área Comercial y Ventas

1. **Cotizaciones y Órdenes de Venta (Pedidos preventa)** *(🔴 Crítico)*:

   - En este negocio, los agentes de ventas visitan talleres mecánicos, flotillas y refaccionarias para levantar pedidos. Actualmente solo existe el POS directo.

   - **Falta:** Entidades `Cotizacion`, `CotizacionDetalle`, `OrdenVenta`. Flujo: *Cotización ➔ Aprobación / Compromiso de precio ➔ Conversión a Pedido / Factura*. Control de vigencia de cotización.

2. **Listas de Precios Diferenciadas y Descuentos por Volumen**:

   - Actualmente existen solo `PrecioVenta`, `PrecioMayoreo` y `PrecioUnitario` en [Producto.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Domain/Entities/Producto.cs).

   - **Falta:** Listas de precios por tipo de cliente (Público, Taller, Refaccionaria, Distribuidor Mayorista) y matriz de descuento por volumen (ej. compra por caja o por tambor).

3. **Buscador de Compatibilidad Automotriz / Guía de Lubricación** *(Diferenciador del sector)*:

   - Los despachadores frecuentemente reciben preguntas como: *"¿Qué aceite lleva un Versa 2020?"* o *"¿Cuántos litros ocupa una Ranger 2018 Diésel?"*.

   - **Falta:** Tablas `VehiculoModelo` y `AceiteCompatibilidad` (Marca, Modelo, Año, Motor, Viscosidad recomendada, Norma OEM, Capacidad de cárter en litros). Permite cotizar y vender en segundos sin error de lubricación.

4. **Límites de Crédito y Control de Clientes Morosos**:

   - En refaccionarias y talleres el crédito a 15, 30 o 45 días es la norma.

   - **Falta:** Campos `LimiteCredito`, `DiasCredito` en `Cliente`, bloqueo automático en POS/Facturación cuando el saldo vencido supera el límite permitido.

### 2. Logística, Almacén y Distribución (Específico de Lubricantes)

1. **Despacho a Granel y Control de "Tambor Abierto"**:

   - El inventario descuenta litros, pero físicamente un distribuidor tiene **tambores cerrados de 208 L** y **tambores abiertos en uso** en mostrador o taller.

   - **Falta:** Diferenciación entre piezas selladas y producto a granel en uso para evitar vender un tambor cerrado cuando solo queda saldo abierto fraccionado.

2. **Módulo de Reparto, Rutas y Choferes**:

   - Las entregas no se recogen siempre en tienda; se envían en camionetas de ruta.

   - **Falta:** Entidades `RutaEntrega`, `VehiculoReparto`, `Chofer`. Generación de hoja de ruta de entrega, remisiones de reparto y liquidación de mercancía entregada vs. devuelta.

3. **Control de Envases Retornables / Depósito de Tambores Vacíos**:

   - Los tambores metálicos de 208 litros vacíos tienen valor comercial y regulación ambiental (residuos peligrosos en México - SEMARNAT). Los distribuidores cobran un depósito o bonifican el tambor vacío devuelto.

   - **Falta:** Control de tambores prestados/entregados a clientes y registro de tambores recuperados.

4. **Complemento Carta Porte 3.0 / 3.1 (CFDI de Traslado / Ingreso)**:

   - Si la empresa transporta aceites e hidrocarburos por tramos de jurisdicción federal en México, la ley fiscal exige Carta Porte con claves de material peligroso / lubricantes.

   - **Falta:** Soporte para CFDI de Traslado con complemento Carta Porte.

### 3. Finanzas, Tesorería y Contabilidad

1. **Gastos Operativos y Caja Chica (`/gastos`)** *(🔴 Crítico)*:

   - Una distribuidora incurre diariamente en gastos: combustible de las camionetas de reparto, mantenimiento mecánico de vehículos, casetas, viáticos, consumibles de oficina.

   - **Falta:** Entidad `Gasto`, pantalla `/gastos`, vinculación con el corte de caja para conciliar los egresos de efectivo y afectación al libro mayor.

2. **Cuentas por Cobrar (CxC) y Cuentas por Pagar (CxP) con Antigüedad de Saldos**:

   - Aunque existen consultas en [ComprasService.Cxp.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/ComprasService.Cxp.cs) y [EstadoCuentasService.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/EstadoCuentasService.cs), no hay gestión de vencimientos.

   - **Falta:** Reporte de antigüedad de saldos (*0-15 días, 16-30 días, 31-60 días, más de 60 días*), cálculo de intereses moratorios y calendario de cobros/pagos.

3. **Conciliación Bancaria**:

   - Los pagos por transferencia electrónica (SPEI) o cheques no están conciliados contra el extracto del banco.

   - **Falta:** Módulo de bancos para cargar o registrar movimientos bancarios y empatarlos con `CompraPago` y `VentaCobro`.

4. **Costo de Ventas por Lote FIFO y Cierre de Período**:

   - [AsientoGeneradorService.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Infrastructure/Services/AsientoGeneradorService.cs) no genera los asientos de costo de venta (Costo de Ventas vs. Almacén) basados en el costo real del lote salido por FIFO.

   - **Falta:** Asientos automáticos de costo de venta y proceso formal de "Cierre Contable Mensual" con bloqueo de periodos cerrados.

5. **Estados Financieros Básicos**:

   - **Falta:** Balance General, Estado de Resultados (Pérdidas y Ganancias por sucursal) y Balanza de Comprobación con base en el catálogo de cuentas existente.

### 4. Operativa, Documentos y Cumplimiento

1. **Exportación de Información (Excel y PDF)** *(🟡 Necesario)*:

   - Actualmente solo existe `window.print` en el ticket y lectura de Excel para SEPOMEX e importación de catálogo.

   - **Falta:** Generación y descarga en Excel (`ClosedXML` o `MiniExcel`) y PDF (`QuestPDF`) para:

     - Estado de cuenta del cliente (para enviar por WhatsApp/correo).

     - Lista de precios para clientes y mecánicos.

     - Representación impresa formal de facturas CFDI con código bidimensional QR SAT.

     - Reportes de inventario y kardex.

2. **Envío Automático de Facturas por Correo Electrónico**:

   - Tras timbrar un CFDI, el cliente debe recibir inmediatamente su archivo `.xml` y `.pdf` por correo electrónico.

   - **Falta:** Servicio SMTP / API de correo (ej. SendGrid o MailKit) integrado a la emisión de facturas.

3. **Matriz Dinámica de Permisos (RBAC)**:

   - Las tablas `Roles`, `Permisos` y `RolPermisos` ya existen en la base de datos, pero la aplicación no cuenta con una interfaz para administrar qué permisos tiene cada rol ni aplica autorización granular a nivel de endpoint o componente.


## Plan de Acción Recomendado por Fases

| Fase | Alcance Principal | Impacto en el Negocio |
| :-: | - | - |
| **Fase 1: Refactorización SOLID & DIP** | Desacoplar `ErpDbContext` de las páginas Blazor ([Productos.razor.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Productos.razor.cs), [Compras.razor.cs](file:///d:/GMS/Jerry/OilsMexico/src/OilsMexico.Web/Components/Pages/ERP/Compras.razor.cs), etc.) creando sus respectivos servicios. Limpiar opciones hardcodeadas en HTML. | Estabilidad del servidor Blazor, eliminación de errores de concurrencia y facilidad de pruebas. |
| **Fase 2: Cotizaciones, Preventa y Crédito** | Implementar `Cotizaciones` y `OrdenesVenta`, listas de precios por tipo de cliente, y límites de crédito con bloqueo por morosidad. | Cierre del ciclo comercial real con talleres y clientes de mayoreo. |
| **Fase 3: Gastos Operativos y Cartera (CxC/CxP)** | Módulo `/gastos` vinculado a caja chica y reporte de antigüedad de saldos a 15/30/45 días. | Cuadre real del flujo de efectivo y mitigación del riesgo financiero. |
| **Fase 4: Exportación y Logística de Aceites** | Exportación a PDF/Excel (estados de cuenta, facturas CFDI con QR) y control de despacho a granel (tambores abiertos vs cerrados). | Operación ágil en mostrador y profesionalismo frente al cliente. |



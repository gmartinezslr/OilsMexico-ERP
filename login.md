# Acceso y control de cuentas

## Comportamiento actual

- `/login` solicita correo o nombre de usuario y contraseña.
- Solo entran cuentas activas y sin bloqueo vigente. La sesión registra usuario, rol, sucursal y un **token de sesión** (columna `usuarios.sesion_token`, emitido en cada login).
- Al restaurar la sesión desde el navegador se revalida contra la base de datos: usuario activo, sin bloqueo vigente y token sin revocar; rol y sucursal se refrescan desde BD para que valgan los permisos vigentes.
- Cerrar sesión elimina la sesión del navegador y navega a `/login` con una recarga completa.
- El menú se filtra por rol **y cada opción coincide con los permisos de su página** (grupo Ventas: Punto de Venta y Corte de caja solo Admin/Vendedor; Facturación, Historial y Notas de crédito también Conta; Complementos de pago solo Admin/Conta). Las páginas comprueban sesión y rol al entrar y redirigen: sin sesión → `/login`, rol sin permiso → `/` (ninguna pantalla queda en blanco).
- **Mensajes unificados (sin enumeración de cuentas):** toda falla de contraseña —cuenta inexistente, contraseña incorrecta o cuenta bloqueada con contraseña equivocada— responde `"Usuario o contraseña incorrectos."`, sin contador de intentos. Cuando la cuenta no existe se verifica además contra un hash ficticio para igualar el tiempo de respuesta. El estado de la cuenta (inactiva, bloqueada con fecha o bloqueo definitivo) solo se revela a quien presenta la contraseña correcta.
- En **Administración > Usuarios**, un administrador puede asignar correo, crear o cambiar contraseña y desbloquear cuentas.
- Las descargas de XML (`/api/facturas|notas-credito|reps/{id}/xml`) y la conexión SignalR (`/hubs/erp`) exigen el token de sesión en la query (`?t=...`): sin él responden 401 o abortan la conexión.
- **Revocación de sesiones:** cambiar la contraseña, desactivar la cuenta o pulsar **Desbloquear acceso** anulan el token, de modo que las sesiones abiertas en otros navegadores se cierran en la siguiente recarga.

| Rol | Menús principales |
| --- | --- |
| Admin | Todos |
| Vendedor | General, Ventas (sin Complementos de pago), Contactos |
| Almacen | General, Almacén |
| Conta | General, Ventas (Facturación, Historial, Notas de crédito, Complementos de pago), Gestión |

La matriz refleja los roles actuales. Al agregar roles o páginas, revisar tanto la visibilidad del menú como los controles de la página.

## Intentos y bloqueos

1. El contador se lleva por cuenta y cada intento fallido se registra con **un único `UPDATE` atómico** (`ExecuteUpdateAsync` con las condiciones en la sentencia, dentro de una transacción que lee el resultado bajo el bloqueo de fila): intentos simultáneos no pueden evadir el límite de tres ni producir un bloqueo doble. Solo cuentan cuentas activas, sin bloqueo vigente y sin bloqueo definitivo.
2. Al tercer intento fallido, la cuenta se bloquea 30 minutos (el contador de intentos vuelve a 0 y sube el contador de bloqueos). El fallo que aplica el bloqueo también responde el mensaje genérico; el usuario se entera del bloqueo cuando prueba con la contraseña correcta.
3. Después de ese primer bloqueo (vencidos los 30 minutos, sin desbloqueo manual), otros tres intentos fallidos producen un bloqueo definitivo.
4. **Decisión de diseño:** un login exitoso limpia el contador de intentos **y el contador de bloqueos**. Un atacante que nunca acierta la contraseña nunca lo limpia, por lo que su segundo episodio de tres fallos sigue siendo definitivo; el usuario legítimo que ya autenticó no queda condenado por bloqueos lejanos.
5. Un administrador selecciona la cuenta en Usuarios y pulsa **Desbloquear acceso**. La operación es un `UPDATE` directo (sin pasar por el change tracker, para que nunca falle por entidades rastreadas con valores obsoletos): se limpian los intentos y el contador de bloqueos y se revoca el token de sesión.
6. El bloqueo definitivo envía un correo si SMTP está configurado. Si faltan ajustes o falla el envío, se registra una advertencia o error en los logs. Solo el intento que aplicó el bloqueo envía la notificación (el `WHERE` de la sentencia excluye cuentas ya definitivas, incluso en intentos concurrentes).

El bloqueo es por usuario, no por dirección IP. Las búsquedas de usuario y correo ignoran mayúsculas y minúsculas.

## Contraseñas y hash

- El hash actual usa **ASP.NET Core Identity `PasswordHasher<Usuario>`** (PBKDF2 con sal y coste adaptativo); no hay paquete NuGet, proviene del shared framework (`FrameworkReference Microsoft.AspNetCore.App`).
- Las cuentas heredadas (SHA-256 sin sal, incluido el PIN legado) se aceptan **solo para migrar**: al hacer login correcto, la contraseña se regenera con `PasswordHasher` y el `PinHash` se limpia.
- El PIN legado solo se acepta cuando la cuenta aún no tiene `PasswordHash`; una cuenta con contraseña moderna ya no entra con el PIN.
- Las cuentas nuevas requieren correo y contraseña de al menos ocho caracteres.

> El seed de instalaciones nuevas todavía crea cuentas demo con PIN de 4 caracteres (`1234`, `1111`, `2222`, `3333`). En esta base de datos esas cuentas fueron eliminadas tras crear los usuarios reales; en producción no debe ejecutarse el seed demo.

## Configuración de correo

Configurar los siguientes valores mediante variables de entorno, user-secrets o un gestor de secretos. No guardar la contraseña SMTP en un `appsettings.json` versionado.

```text
Auth__CorreoAdministrador=admin@empresa.mx
Email__SmtpHost=smtp.empresa.mx
Email__Port=587
Email__EnableSsl=true
Email__Username=remitente@empresa.mx
Email__Password=<secreto SMTP>
Email__From=remitente@empresa.mx
```

El usuario y la contraseña SMTP pueden omitirse solo si el servidor permite envío sin autenticación. `Email__From` debe ser una dirección autorizada por el servidor. Probar la entrega desde el entorno desplegado y revisar los logs ante errores.

## Estado de las recomendaciones

| # | Recomendación | Estado |
| --- | --- | --- |
| 1 | Hash de contraseñas con Identity `PasswordHasher` | ✅ Hecho; los hashes heredados se migran automáticamente en el primer login |
| 2 | Retirar accesos demo y PIN legacy | ⚠️ Parcial: eliminados de esta BD; el seed de instalaciones nuevas sigue creándolos |
| 3 | Proteger endpoints HTTP y SignalR | ✅ Hecho: descargas XML y hub exigen token de sesión vigente |
| 4 | Migrar a autenticación del framework (Identity/cookies) | ⚠️ Pendiente; hoy se usa `ProtectedSessionStorage`, pero la sesión se revalida contra BD y el token se revoca |
| 5 | Hacer atómico el contador de intentos | ✅ Hecho: `UPDATE` condicional único en transacción; verificado con 8 intentos fallidos en paralelo |
| 6 | Limitar intentos por IP y unificar mensajes | ⚠️ Parcial: mensajes unificados y sin enumeración (hecho); el rate-limit por IP sigue pendiente |
| 7 | Revocar sesiones ante cambios | ✅ Hecho vía token (desbloqueo, desactivación, cambio de contraseña) |
| 8 | Pruebas automatizadas de seguridad | ⚠️ Parcial: guion de aprobación/pruebas en vivo que cubre login, mensajes, bloqueos, paralelismo y desbloqueo; aún no hay proyecto de pruebas unitarias en la solución |

## Archivos relacionados

- `src/OilsMexico.Web/Components/Pages/ERP/Login.razor`: formulario de acceso.
- `src/OilsMexico.Infrastructure/Services/AuthService.cs`: validación con `PasswordHasher`, migración de hash heredado, intentos, bloqueos, correo, token de sesión y desbloqueo.
- `src/OilsMexico.Domain/Entities/Usuario.cs`: correo, hashes, estado de bloqueo y `SesionToken`.
- `src/OilsMexico.Infrastructure/Persistence/SchemaPatch.cs`: cambios idempotentes al esquema PostgreSQL (incluye `sesion_token`).
- `src/OilsMexico.Infrastructure/Persistence/Configs/VentaConfigs.cs`: mapeo de columnas de `usuarios`.
- `src/OilsMexico.Web/Services/SesionActual.cs`: revalidación de la sesión contra BD al restaurarla.
- `src/OilsMexico.Web/Program.cs`: guardia de token en las descargas XML.
- `src/OilsMexico.Web/Hubs/ErpHub.cs`: validación de token al conectar SignalR.
- `src/OilsMexico.Web/Components/Pages/ERP/Usuarios.razor` y `.razor.cs`: administración de usuarios y desbloqueo.
- `src/OilsMexico.Web/Components/Layout/NavMenu.razor`: visibilidad del menú por rol, con permisos por enlace del grupo Ventas.
- `src/OilsMexico.Web/Components/Pages/ERP/EstadoCuentas.razor.cs` y `VentasGestion.razor.cs`: redirección a `/login` o `/` según sesión y rol.

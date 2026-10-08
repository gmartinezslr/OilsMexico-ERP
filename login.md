# Acceso y control de cuentas

## Comportamiento actual

- `/login` solicita correo o nombre de usuario y contraseña.
- Solo entran cuentas activas y sin bloqueo vigente. La sesión registra usuario, rol, sucursal y un **token de sesión** (columna `usuarios.sesion_token`, emitido en cada login).
- Al restaurar la sesión desde el navegador se revalida contra la base de datos: usuario activo, sin bloqueo vigente y token sin revocar; rol y sucursal se refrescan desde BD para que valgan los permisos vigentes.
- Cerrar sesión elimina la sesión del navegador y navega a `/login` con una recarga completa.
- El menú se filtra por rol y las páginas ERP comprueban sesión y rol al entrar.
- En **Administración > Usuarios**, un administrador puede asignar correo, crear o cambiar contraseña y desbloquear cuentas.
- Las descargas de XML (`/api/facturas|notas-credito|reps/{id}/xml`) y la conexión SignalR (`/hubs/erp`) exigen el token de sesión en la query (`?t=...`): sin él responden 401 o abortan la conexión.
- **Revocación de sesiones:** cambiar la contraseña, desactivar la cuenta o pulsar **Desbloquear acceso** anulan el token, de modo que las sesiones abiertas en otros navegadores se cierran en la siguiente recarga.

| Rol | Menús principales |
| --- | --- |
| Admin | Todos |
| Vendedor | General, Ventas, Contactos |
| Almacen | General, Almacén |
| Conta | General, Gestión |

La matriz refleja los roles actuales. Al agregar roles o páginas, revisar tanto la visibilidad del menú como los controles de la página.

## Intentos y bloqueos

1. El contador se lleva por cuenta. Al tercer intento fallido, la cuenta se bloquea 30 minutos.
2. Después de ese primer bloqueo (vencidos los 30 minutos, sin desbloqueo manual), otros tres intentos fallidos producen un bloqueo definitivo.
3. Un administrador selecciona la cuenta en Usuarios y pulsa **Desbloquear acceso**. Se limpian los intentos y el contador de bloqueos y se revoca el token de sesión.
4. El bloqueo definitivo envía un correo si SMTP está configurado. Si faltan ajustes o falla el envío, se registra una advertencia o error en los logs.

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
| 5 | Hacer atómico el contador de intentos | ⚠️ Pendiente: sigue siendo read-modify-write (evadir con intentos simultáneos sigue siendo posible) |
| 6 | Limitar intentos por IP y unificar mensajes | ⚠️ Pendiente |
| 7 | Revocar sesiones ante cambios | ✅ Hecho vía token (desbloqueo, desactivación, cambio de contraseña) |
| 8 | Pruebas automatizadas de seguridad | ⚠️ Pendiente; la validación actual fue manual con un script de aprovisionamiento |

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
- `src/OilsMexico.Web/Components/Layout/NavMenu.razor`: visibilidad del menú por rol.

# Validación P0 — SMTP extremo a extremo (15-oct-2026)

> Pre-requisito: cuenta SMTP de pruebas configurada y buzón de destino para verificar recepción.
> Las credenciales SMTP se configuraron en **user-secrets local** (no se commitean al repositorio).

## Credenciales configuradas (user-secrets, solo local)

| Clave | Valor configurado |
|-------|-------------------|
| `Correo:Habilitado` | `1` |
| `Correo:Host` | `smtp.gmail.com` |
| `Correo:Puerto` | `587` |
| `Correo:Ssl` | `1` (true) |
| `Correo:Usuario` | `gmartinezslr@gmail.com` |
| `Correo:Password` | *(configurada, no se muestra por seguridad)* |
| `Correo:Remitente` | `gmartinezslr@gmail.com` |
| `Correo:RemitenteNombre` | `Administrador` |

> ⚠️ **Seguridad:** La contraseña de aplicación de Gmail está en user-secrets local. Nunca se sube al repositorio. Si necesitas rotarla, usa `dotnet user-secrets set "Correo:Password" "<nueva>"`.

## Cambio de código aplicado (Opción B de negocio)

**Archivo:** `src/OilsMexico.Infrastructure/Services/EmailService.Documentos.cs`

`EnviarCfdiAsync` ahora requiere **tanto XML como PDF** del CFDI. Si `FacturaCfdiPdfAsync` falla, el envío lanza excepción y **no se considera exitoso** — el usuario debe saberlo. Ya no hay respaldo defensivo de "solo XML".

## Casos de validación (botón "Probar" + envíos reales a un buzón de pruebas)

### Paso 1 — Configurar en la UI
- Ir a **Configuración → Correo saliente** (solo Admin)
- Verificar que los valores de user-secrets se carguen correctamente en la pantalla
- Habilitar el envío de correos

### Paso 2 — Conexión SMTP (`ProbarAsync`)
- Ejecutar el botón **"Probar"** en Configuración → Correo saliente
- **Esperado:** correo de prueba recibido en `gmartinezslr@gmail.com` con asunto `[OilsMexico] Prueba de correo SMTP`

### Paso 3 — CFDI con XML + PDF obligatorio (`EnviarCfdiAsync`)
- Ir a **Facturación**, seleccionar una factura timbrada (estado `Timbrada`)
- Pulsar **"Enviar CFDI"** (o el botón de envío de correo de la factura)
- **Esperado:** correo recibido en el buzón de destino con:
  - Adjunto `.xml` (comprobante fiscal)
  - Adjunto `.pdf` (representación impresa con QR SAT, sello, cadena original)

### Paso 4 — Estado de cuenta cliente (CxC)
- Ejecutar `EnviarEstadoCuentaClienteAsync(clienteId)` para un cliente con saldo pendiente
- **Esperado:** correo con tabla de facturas y cobros, saldo pendiente correcto

### Paso 5 — Estado de cuenta proveedor (CxP)
- Ejecutar `EnviarEstadoCuentaProveedorAsync(proveedorId)` para un proveedor con compras pendientes
- **Esperado:** correo con tabla de órdenes de compra y pagos, saldo total correcto

### Paso 6 — Comunicado masivo (`EnviarComunicadoAsync`)
- Ejecutar `EnviarComunicadoAsync` con asunto y cuerpo de prueba
- **Esperado:** correos recibidos por todos los destinatarios seleccionados

## Resultado esperado global

| Verificación | Criterio de aceptación |
|-------------|----------------------|
| Recepción en buzón | ✅ Todos los correos llegan al destinatario |
| Remitente correcto | ✅ `Administrador <gmartinezslr@gmail.com>` |
| Adjunto XML del CFDI | ✅ Presente en envío de factura |
| Adjunto PDF del CFDI | ✅ Presente (obligatorio, Opción B) |
| QR SAT visible en PDF | ✅ PDF muestra QR con URL de verificación SAT |
| Fallo cerrado si correo deshabilitado | ✅ `ValidarListoParaEnviar` lanza excepción en español |

## Cómo ejecutar las pruebas

1. **Cerrar el ERP** si está ejecutándose (bloquea los DLL al compilar)
2. **Compilar:** `dotnet build OilsMexico.slnx` desde la raíz del proyecto
3. **Ejecutar el ERP:** `dotnet run --project src/OilsMexico.Web/OilsMexico.Web.csproj`
4. **Iniciar sesión** como usuario Admin
5. **Configurar Correo saliente** en Configuración (verificar que user-secrets cargó)
6. **Ejecutar cada caso** de validación arriba y verificar recepción en el buzón

> Nota: Si el build falla por DLLs bloqueados, cerrar la instancia del ERP y volver a compilar.

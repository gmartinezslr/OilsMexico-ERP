# Validación P0 — SMTP extremo a extremo (15-oct-2026)

> Pre-requisito: cuenta SMTP de pruebas (host/puerto/usuario/password).
> Se configura en Configuración → Correo saliente (solo Admin); nada va al repo.

## Casos (botón "Probar" + envíos reales a un buzón de pruebas)

1. **Conexión**: `ProbarAsync()` → correo de prueba al remitente.
2. **CFDI**: `EnviarCfdiAsync(facturaId)` → verificar XML + PDF adjuntos.
3. **Estados de cuenta**: `EnviarEstadoCuentaClienteAsync` y
   `EnviarEstadoCuentaProveedorAsync` → verificar tablas de saldos.
4. **Comunicado**: `EnviarComunicadoAsync` → verificar destinatarios.

## Resultado esperado

Recepción en el buzón, remitente correcto, adjuntos XML + PDF del CFDI.
Con correo deshabilitado el servicio falla cerrado con mensaje en español
(verificado por código el 15-oct: `ValidarListoParaEnviar`).

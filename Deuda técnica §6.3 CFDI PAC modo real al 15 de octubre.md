# Deuda técnica §6.3 — CFDI / PAC modo real al 15 de octubre

Estado de la sección **6.3** del `Checklist completitud ERP.md`, verificado contra el código real del repositorio el 15/10/2026.

> ⚠️ **Advertencia de seguridad:** nunca compartas por este canal tus certificados CSD (`.cer` / `.key`), contraseñas, user-secrets ni credenciales de Finkok. Tú debes colocarlos localmente en tu máquina; yo solo documento los pasos.

---

## Resumen

| Ítem | Descripción | Estado |
|------|-------------|--------|
| 6.3.1 | Colocar `certs/csd.cer` + `certs/csd.key` (local, nunca al repo) | ⏳ Pendiente (tú) |
| 6.3.2 | Configurar `user-secrets`: `Cfdi:KeyPassword`, `Cfdi__Pac__Usuario`, `Cfdi__Pac__Password`, `Cfdi:PacModo=TEST` | ⏳ Pendiente (tú) |
| 6.3.3 | Verificar `PacTimbradoService` modos SIMULADO / TEST / PRODUCCION | ✅ Código listo |
| 6.3.4 | Prueba Finkok TEST: timbrar factura PUE | ⏳ Pendiente (tú) |
| 6.3.5 | Prueba Finkok TEST: timbrar factura PPD + complemento de pago REP | ⏳ Pendiente (tú) |
| 6.3.6 | Prueba Finkok TEST: cancelar con motivo 02 (y 01 con UUID sustituto) | ⏳ Pendiente (tú) |
| 6.3.7 | Prueba Finkok TEST: consultar `EstadoSatAsync` (`get_sat_status`) | ⏳ Pendiente (tú) |
| 6.3.8 | Verificar UI `/facturacion` muestra modo PAC real (no SIMULADO) | ⏳ Pendiente (tú) |

## 6.3.1 — Colocar CSD (`.cer` + `.key`) en `certs/`

**Estado:** ⏳ **PENDIENTE — requiere acción tuya**

**Qué hacer:**
1. Crear la carpeta `certs/` en la raíz del proyecto (está en `.gitignore`, nunca se sube al repo).
2. Colocar tu archivo de certificado CSD del SAT:
   - `certs/csd.cer` (certificado público)
   - `certs/csd.key` (llave privada)
3. Verificar que `appsettings.json` apunte a estas rutas (ya configurado):

```json
"Cfdi": {
## 6.3.3 — Verificar `PacTimbradoService` modos SIMULADO / TEST / PRODUCCION

**Estado:** ✅ **CÓDIGO LISTO** (no requiere cambios de código)

**Qué ya existe en el código:**
- `PacTimbradoService` con modos `SIMULADO` / `TEST` / `PRODUCCION`
- `DatosPac` fail-closed: si no hay credenciales, no intenta timbrar real
- Reintento Finkok 307 (redirect)
- `cancel_signature` firmada localmente (la llave nunca viaja al PAC)
- `EstadoSatAsync` vía `get_sat_status`
- UI `/facturacion` muestra el modo PAC (`pacModo`)

**Qué verificar:**
- Que `PacModo=TEST` no lance `NotImplementedException`.
- Que sin CSD ni credenciales, caiga gracefully a `SIMULADO`.

## 6.3.4 — Prueba Finkok TEST: timbrar factura PUE

**Estado:** ⏳ **PENDIENTE — requiere acción tuya (BD + Finkok TEST)**

**Escenario:**
1. Levantar PostgreSQL local con datos de prueba (seed del ERP).
2. Iniciar sesión como Admin o Vendedor.
3. Ir a `/ventas-pos`, registrar una venta con forma de pago `01 = Efectivo` (PUE = Pago en una sola exhibición).
4. Toggle CFDI activado.
5. Ir a `/facturacion`, buscar la factura en estado `Pendiente`.
6. Pulsar "Timbrar".
7. Verificar que la factura pase a `Timbrada`, que tenga `UuidSat` y `XmlSellado` poblados.
8. Descargar XML por `/api/facturas/{id}/xml` y validar que tiene el Timbre Fiscal Digital (TFD) del SAT.

**Criterio de éxito:** UUID real de Finkok TEST (no un GUID local), XML sellado con sello del PAC.

## 6.3.5 — Prueba Finkok TEST: timbrar factura PPD + complemento de pago REP

**Estado:** ⏳ **PENDIENTE — requiere acción tuya (BD + Finkok TEST)**

**Escenario:**
1. Crear una venta con forma de pago `PPD` (Pago en Parcialidades o Diferido) — no se paga al contado.
2. Timbrar la factura PPD (debe timbrarse como factura relacionada).
3. Ir a `/complementos-pago` o desde la factura, registrar un cobro parcial (REP — Recibo de Efectivo / Pagos 2.0).
4. Timbrar el complemento de pago (Tipo P + Pagos 2.0).
5. Verificar que el REP tenga UUID propio y esté vinculado a la factura PPD original.

**Criterio de éxito:** Factura PPD timbrada + complemento de pago P timbrado, ambos con UUID de Finkok TEST.

  "CerPath": "certs/csd.cer",
  "KeyPath": "certs/csd.key",
## 6.3.6 — Prueba Finkok TEST: cancelar con motivo 02 (y 01 con UUID sustituto)

**Estado:** ⏳ **PENDIENTE — requiere acción tuya (BD + Finkok TEST)**

**Escenario:**
1. Tener una factura timbrada en Finkok TEST.
2. Ir a `/facturacion`, pulsar "Cancelar CFDI".
3. Seleccionar motivo `02 = Sin relación`.
4. Confirmar cancelación. Verificar que la factura pase a `Cancelada` y que `motivo_cancelacion` se guarde.
5. (Opcional, solo Admin/Conta) Probar motivo `01 = Con relación (exige sustituto)`:
   - Debe exigir UUID del CFDI sustituto.
   - Sin UUID sustituto, debe bloquear.
   - Con UUID sustituto válido, cancelar correctamente.

**Criterio de éxito:** Cancelación real ante Finkok (no solo marcado local). El CSD firma el `cancel_signature` localmente; la llave privada nunca sale de tu máquina.

## 6.3.7 — Prueba Finkok TEST: consultar `EstadoSatAsync` (`get_sat_status`)

**Estado:** ⏳ **PENDIENTE — requiere acción tuya (BD + Finkok TEST)**

**Escenario:**
1. Tener una factura timbrada con UUID real de Finkok TEST.
2. Desde el servicio o UI, consultar `EstadoSatAsync(uuid)`.
3. Verificar que devuelve el estado ante el SAT (Vigente, Cancelada, No Encontrado).
4. Probar con un UUID inexistente (debe devolver "No Encontrado" o similar sin lanzar excepción no controlada).

**Criterio de éxito:** Consulta al SAT vía Finkok TEST responde correctamente.
---

## Archivos de código involucrados (no se modifican en esta sesión)

| Archivo | Responsabilidad |
|---------|-----------------|
| `src/OilsMexico.Web/appsettings.json` | Claves `Cfdi:CerPath`, `KeyPath`, `KeyPassword`, `PacModo`, `Pac.Usuario`, `Pac.Password` |
| `src/OilsMexico.Infrastructure/Services/CfdiServices.cs` | Sellado RSA-SHA256, timbrado PAC, cancelación |
| `src/OilsMexico.Infrastructure/Services/FacturacionService.cs` | Timbrar facturas, cancelar facturas |
| `src/OilsMexico.Infrastructure/Services/NotaCreditoService.*` | Timbrar NC (Tipo E, relación 01) |
| `src/OilsMexico.Infrastructure/Services/ComplementoPagoService.*` | Timbrar REP (Tipo P, Pagos 2.0) |
| `src/OilsMexico.Web/Components/Pages/ERP/Facturacion.razor(.cs)` | UI de facturación, muestra `pacModo`, timbrar/cancelar |
| `src/OilsMexico.Web/Components/Pages/ERP/NotasCredito.razor(.cs)` | UI de notas de crédito |

## Decisión de arquitectura (ya tomada)

Mantener `CatalogosSatService` en código (diccionarios estáticos en `ContextoCatalogos.cs`). Los catálogos SAT son oficiales y estables; moverlos a tabla `catalogos_sat` añadiría complejidad sin beneficio tangible.

---

## Siguiente paso para ti

1. Coloca `certs/csd.cer` + `certs/csd.key` en la carpeta `certs/` (local).
2. Configura `user-secrets` con tus credenciales Finkok TEST y `Cfdi:PacModo=TEST`.
3. Ejecuta las pruebas 6.3.4 a 6.3.8 contra Finkok TEST.
4. Cuando hayas verificado que todo funciona, avísame para marcar el §6.3 como ✅ Cerrado en este checklist.

¿Quieres que te ayude con algún paso específico de la configuración o de las pruebas contra Finkok TEST?


## 6.3.8 — Verificar UI `/facturacion` muestra modo PAC real

**Estado:** ⏳ **PENDIENTE — requiere acción tuya**

**Qué verificar:**
1. Abrir `/facturacion`.
2. En el badge superior derecho debe mostrar `PAC Finkok: TEST` (no `SIMULADO`).
3. Si sigue en `SIMULADO`, revisar que `user-secrets` tenga `Cfdi:PacModo=TEST` correctamente cargado.
4. Verificar que el botón "Timbrar" intente timbrado real y no devuelva UUID local inmediato.

  "KeyPassword": "",
  "PacModo": "SIMULADO",
  "Pac": {
    "Usuario": "",
    "Password": ""
  }
}
```

**Por qué:** Sin CSD real, el sellado RSA-SHA256 no puede firmar el CFDI y el sistema se queda en modo `SIMULADO` (UUID local auditable, solo facturas).

## 6.3.2 — Configurar `user-secrets` (credenciales Finkok TEST)

**Estado:** ⏳ **PENDIENTE — requiere acción tuya**

**Qué hacer (en PowerShell, desde la carpeta del proyecto):**

```powershell
dotnet user-secrets init --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi__Pac__Usuario" "TU_USUARIO_FINKOK_TEST" --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi__Pac__Password" "TU_PASSWORD_FINKOK_TEST" --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi:PacModo" "TEST" --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi:KeyPassword" "TU_CSD_PASSWORD" --project src/OilsMexico.Web
```

**Por qué:** Las credenciales nunca deben ir en `appsettings.json` ni commitearse al repo. `user-secrets` las guarda fuera del repositorio.

**Verificación:** Después de esto, `Config["Cfdi:PacModo"]` debe devolver `"TEST"` y el servicio PAC debe intentar timbrar contra Finkok TEST en lugar de generar UUID local.

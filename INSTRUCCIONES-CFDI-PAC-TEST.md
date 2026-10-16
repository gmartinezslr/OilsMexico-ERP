# Instrucciones: CFDI / PAC modo TEST (Finkok) — §6.3

Estado: El código ya soporta modo TEST. Solo falta que **tú** configures CSD + credenciales y pruebes contra Finkok TEST.

---

## 1. Qué YA está implementado (no toca código)

| Componente | Archivo | Estado |
|------------|---------|--------|
| Sellado RSA-SHA256 nativo CFDI 4.0 | `CfdiServices.cs` (`CfdiSelladoService`) | ✅ Listo |
| Timbrado vía PAC (SIMULADO / TEST / PRODUCCION) | `CfdiServices.cs` (`PacTimbradoService`) | ✅ Listo |
| Cancelación `cancel_signature` firmada localmente | `CfdiServices.cs` | ✅ Listo |
| Consulta `EstadoSatAsync` (`get_sat_status`) | `CfdiServices.cs` | ✅ Listo |
| UI `/facturacion` muestra modo PAC | `Facturacion.razor` | ✅ Listo |
| UI `Notas de Crédito` (Tipo E, relación 01) | `NotasCredito.razor(.cs)` | ✅ Listo |
| UI Complemento de Pago (Tipo P, Pagos 2.0) | `ComplementoPagoService.*` | ✅ Listo |

**No hay `NotImplementedException` en modo TEST.** El código está listo para recibir `Cfdi:PacModo=TEST`.

---

## 2. Qué DEBES hacer tú (no puedo hacerlo por ti)

### Paso A — Colocar el CSD (tus certificados del SAT)

1. Crea la carpeta `certs/` en la raíz del proyecto (ya está en `.gitignore`).
2. Coloca tus archivos:
   - `certs/csd.cer` — certificado público del SAT
   - `certs/csd.key` — llave privada del SAT

> ⚠️ Nunca subas estos archivos al repositorio. `.gitignore` ya los bloquea.

### Paso B — Configurar user-secrets (credenciales Finkok TEST)

Ejecuta el script que creé para ti:

```powershell
# Desde la raíz del proyecto:
.\configurar-cfdi-pac-test.ps1
```

O manualmente:

```powershell
dotnet user-secrets init --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi__Pac__Usuario" "TU_USUARIO" --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi__Pac__Password" "TU_PASSWORD" --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi:PacModo" "TEST" --project src/OilsMexico.Web
# Si tu .key tiene contraseña:
dotnet user-secrets set "Cfdi:KeyPassword" "TU_CSD_PASSWORD" --project src/OilsMexico.Web
```

### Paso C — Build y ejecutar

```powershell
dotnet build OilsMexico.slnx --no-restore
dotnet run --project src/OilsMexico.Web --urls "http://localhost:5200"
```

---

## 3. Pruebas a ejecutar contra Finkok TEST

| # | Prueba | Qué hacer | Criterio de éxito |
|---|--------|-----------|-------------------|
| 6.3.4 | Timbrar factura PUE | Venta en POS con forma `01=Efectivo` → timbrar en `/facturacion` | UUID real de Finkok TEST, XML con TFD |
| 6.3.5 | Timbrar PPD + REP | Venta con `PPD` → timbrar → cobro parcial (REP) → timbrar complemento | Ambos con UUID de Finkok TEST |
| 6.3.6 | Cancelar motivo 02 | Factura timbrada → cancelar con motivo `02` | Factura pasa a `Cancelada` ante Finkok |
| 6.3.6b | Cancelar motivo 01 | Factura timbrada → cancelar con `01` + UUID sustituto | Se exige UUID sustituto, se cancela |
| 6.3.7 | Consultar estado SAT | `EstadoSatAsync(uuid)` | Devuelve Vigente/Cancelada/No Encontrado |
| 6.3.8 | Ver UI modo PAC | Abrir `/facturacion` | Badge muestra `PAC Finkok: TEST` |

---

## 4. Script de configuración creado

`d:\GMS\Jerry\OilsMexico\configurar-cfdi-pac-test.ps1`

Este script:
- Verifica que estás en la raíz del proyecto
- Crea la carpeta `certs/` si no existe
- Inicializa `user-secrets` para `OilsMexico.Web`
- Pide interactivamente tus credenciales (no se muestran en pantalla)
- Las guarda en `user-secrets` (fuera del repo)
- Ejecuta `dotnet build` para validar que todo compila

---

## 5. Seguridad (importante)

- **Nunca compartas** tu `csd.key`, contraseñas ni credenciales de Finkok por este chat ni por ningún otro.
- Los `user-secrets` se guardan en tu perfil de usuario de Windows, no en el repositorio.
- La carpeta `certs/` está en `.gitignore` y nunca se commitea.
- Si sospechas que una credencial quedó expuesta, revócala y genera una nueva.

---

## 6. ¿Ya terminaste?

Cuando hayas verificado las 7 pruebas contra Fink OK TEST, avísame y marco el §6.3 como ✅ Cerrado en el checklist `Deuda técnica §6.3 CFDI PAC modo real al 15 de octubre.md`.

# Validación P0 — Finkok TEST (15-oct-2026)

> Pre-requisitos (NO van al repo): `certs/csd.cer` + `certs/csd.key` (CSD de
> pruebas vigente), `Cfdi:KeyPassword` y credenciales Finkok TEST por user-secrets.

```powershell
dotnet user-secrets set "Cfdi:KeyPassword" "..." --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi__Pac__Usuario" "..." --project src/OilsMexico.Web
dotnet user-secrets set "Cfdi__Pac__Password" "..." --project src/OilsMexico.Web
# appsettings o variable de entorno: Cfdi:PacModo = TEST
```

## Casos (en /facturacion con datos fiscales completos: emisor + receptor)

1. **PUE**: timbrar factura PUE → verificar UUID SAT y XML descargable.
2. **PPD + REP**: timbrar factura PPD → registrar cobro parcial (REP Pagos 2.0)
   → verificar XML del REP.
3. **Cancelación**: cancelar con motivo `02` → verificar `MotivoCancelacion`.
4. **SAT**: consultar `EstadoSatAsync(uuid)` → registrar respuesta.

## Resultado esperado

UUID real de Finkok TEST, XML/PDF descargables, cancelación con CodEstatus 201
y estado SAT consultable. Sin CSD/credenciales el sistema queda en SIMULADO
(sello DEV auditable, solo facturas) — verificado por código el 15-oct.

# Avance al 15 de octubre y lo que falta

## Resumen ejecutivo

| Área | Estado |
|------|--------|
| **ERP núcleo operativo** (ventas, almacén, compras, CFDI, contabilidad, caja, reportes) | ~90 % completado |
| **ERP "completo"** (incluye bloqueantes funcionales, deuda técnica, diferenciadores opcionales y verificación/documentación) | ~85 % completado |
| **Faltante total** | **~15 %** |

> Nota: el 15 % restante es cierre de extremos sueltos (bloqueo por morosidad, adjuntar PDF del CFDI al correo, auditoría extendida, deuda técnica de UI/SOLID, pruebas externas con Finkok TEST y SMTP real, documentación). La base de código compila con 0 errores.

---

## ✅ §6.3 CFDI / PAC modo real — COMPLETADO

**Fecha:** 15/10/2026  
**UUID obtenido:** `1db6bcad-89ce-5f52-852f-8f71ea19bb91`  
**Factura timbrada:** `V-1-20261016001552`

### Qué se hizo
- Certificado válido: `CSD_Sucursal_1_EKU9003173C9_20230517_223850.cer` con `csd.key` (par validado por modulus)
- `user-secrets` configurados: `Cfdi:KeyPassword`, `Cfdi::__Pac__Usuario`, `Cfdi::__Pac__Password`, `Cfdi:PacModo=TEST`
- `Cfdi:CerPath` = `D:\GMS\Jerry\OilsMexico\certs\CSD_Sucursal_1_EKU9003173C9_20230517_223850.cer`
- Timbrado exitoso contra `https://demo-facturacion.finkok.com/servicios/soap/stamp`

### Problemas resueltos durante §6.3
| Error | Causa | Solución |
|-------|-------|----------|
| **738** (schemaLocations/namespaces/prefijos) | Declaraciones de namespace redundantes en cada elemento hijo | Declarar namespaces SOLO en la raíz `<cfdi:Comprobante>` |
| **712** (noCertificado no corresponde al certificado) | `csd.cer` no era el par de `csd.key` | Usar `CSD_Sucursal_1_...cer` (el .key es de ese certificado) |
| **CFDI40102** (sello no coincide con cadena original) | `doc.ToString()` serializaba con redeclaraciones de xmlns | Usar `SerializarConPrefijoCfdi` (XML limpio) para calcular cadena original |
| **CFDI40149** (DomicilioFiscalReceptor ≠ LugarExpedicion) | Campo faltante o vacío en receptor | Agregar `DomicilioFiscalReceptor="20928"` (igual a LugarExpedicion) |
| **CFDI40161** (UsoCFDI inválido para el receptor) | `G03` no válido para XAXX010101000 con régimen 616 | Para XAXX010101000: `UsoCFDI="S01"`, `RegimenFiscalReceptor="601"` |

### Pendiente inmediato (opcional, §6.3 extendido)
- Timbrar factura PPD con complemento de pago REP
- Cancelación con motivo 02
- `EstadoSatAsync` (`get_sat_status`)

---

## 🔴 PRIORIDAD ALTA — bloqueantes funcionales

### T-01. Bloqueo por morosidad (§6.1)
Validar que un cliente con morosidad no pueda generar facturas nuevas.

### T-02. Cierre de periodo contable (§6.1)
Impedir asientos en periodos cerrados.

### T-03. Adjuntar PDF del CFDI al envío de correo
Hoy `EnviarCfdiAsync` adjunta solo el XML. Falta generar PDF con QuestPDF y adjuntar ambos.

### T-04. Ampliar `AuditLog` a acciones críticas de UI

### T-05. Pruebas externas reales: Finkok TEST + CSD
**✅ PARCIALMENTE HECHO:** Timbrado PUE exitoso. Falta: PPD + REP, cancelación, `EstadoSatAsync`.

---

## 🟡 PRIORIDAD MEDIA — deuda técnica y verificación operativa

### T-07. Eliminar hardcode remanente en UI (§6.1)
Iterar `Roles.Todos`, consumir `ICatalogosSatService`, centralizar motivos de cancelación, calcular IVA con `Impuestos.TasaIva`, mostrar estado real del PAC, mover catálogos SAT a tabla `catalogos_sat`.

### T-08. Deuda técnica SOLID / DRY / mantenibilidad (§6.2)
Helper único de sucursal, DIP en `Metas.razor.cs`/`VentasPOS.razor.cs`, ISP en `ICotizacionesService`/`IVentasService`, parámetros sin uso en asientos, guard de sucursal en lecturas, atomicidad en `ConvertirOrdenAVentaAsync`, homogeneizar `Estado`, DRY `CalcularTotales`, enum `EstadoFactura`.

### T-09. Verificación manual con BD levantada (§7)
Login 2FA, alta/edición cliente, escaneo barras POS, cierre/reapertura comisión, conciliación bancaria, cotizaciones/OV, reportes financieros, cierre de periodo.

---

## 🟢 PRIORIDAD BAJA — diferenciados opcionales

- Carta Porte 3.x
- Listas de precios, lubricación, rutas, envases

---

## Siguiente paso recomendado

§6.3 está cerrado. Continuar con T-01 (bloqueo por morosidad) o T-03 (adjuntar PDF del CFDI al correo).
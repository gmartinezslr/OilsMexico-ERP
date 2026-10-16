# Deuda técnica SOLID / DRY / Mantenibilidad al 15 de octubre — §6.2

Estado de cada ítem de la sección **6.2** del `Checklist completitud ERP.md`, verificado contra el código real del repositorio el 15/10/2026.

---

## Resumen

| Ítem | Descripción | Estado |
|------|-------------|--------|
| 6.2.1 | B8 — Helper único de sucursal (`ResolverSucursalAutorizada`) | ✅ Hecho |
| 6.2.2 | DIP — `Metas.razor.cs` / `VentasPOS.razor.cs` sin `ErpDbContext` | ✅ Hecho |
| 6.2.3 | ISP Cotizaciones — `ICotizacionesConsulta` + `ICotizacionesOperaciones` | ✅ Hecho |
| 6.2.4 | ISP Ventas — `IVentasPosService` / `IEntregaAlmacenService` / `IHistorialVentasService` | ✅ Hecho |
| 6.2.5 | `IAsientoGeneradorService` — `sucursalId` usado en Compra/Devolución | ✅ Hecho |
| 6.2.6 | REGLA #1 en lecturas — `ObtenerCotizacionAsync` / `ObtenerOrdenVentaAsync` filtran | ✅ Hecho |
| 6.2.7 | Atomicidad — `ConvertirOrdenAVentaAsync` en transacción | ✅ Hecho |
| 6.2.8 | Consistencia de estados — `Estado` enum vs string | ✅ Hecho |
| 6.2.9 | DRY — `CalcularTotales` un único punto de cálculo | ✅ Hecho |
| 6.2.10 | A9 — `EstadoFactura` enum en lugar de literales aislados | ✅ Hecho |

**Conclusión:** Toda la sección §6.2 ya está implementada. Los checklists anteriores referenciaban estados de versiones previas del código; las correcciones ya fueron aplicadas.

## 6.2.1 — B8 — Helper único de sucursal

**Estado:** ✅ **HECHO**

**Ubicación:** `src/OilsMexico.Application/Interfaces/Servicios.cs`, líneas 29-35

```csharp
int ResolverSucursalAutorizada(int sucursalSolicitada) => EsAdmin ? sucursalSolicitada : SucursalId;
## 6.2.7 — Atomicidad — `ConvertirOrdenAVentaAsync` en transacción

**Estado:** ✅ **HECHO**

**Ubicación:** `src/OilsMexico.Infrastructure/Services/VentasService.Venta.cs`, líneas 20-43

```csharp
public async Task<OrdenVentaFacturarResult> FacturarOrdenVentaAsync(...)
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    // ... crea factura, detalle, stock, kardex, póliza, vínculo con OV ...
    // todo dentro de la transacción
}
```

También `ConvertirCotizacionAsync` (`CotizacionesService.Convertir.cs`, líneas 16-73) ya usa `BeginTransactionAsync`.

✅ Cerrado.

## 6.2.8 — Consistencia de estados — `Estado` enum vs string

**Estado:** ✅ **HECHO**

**Verificación:**
- `EstadoFactura` es `public enum EstadoFactura` en `src/OilsMexico.Domain/Enums/ErpEnums.cs`
- `EstadoCompra` es `public enum EstadoCompra` en `src/OilsMexico.Domain/Enums/ErpEnums.cs`
- `EstadoRep` es `public enum EstadoRep` en `src/OilsMexico.Domain/Enums/ErpEnums.cs`
- `EstadoOrdenVenta` y `EstadoCotizacion` están en `src/OilsMexico.Domain/Enums/CotizacionEnums.cs`

`ComplementoPagoService.Cobro.cs` usa `EstadoRep.Pendiente` / `EstadoRep.Timbrado` (enums, no literales string).

✅ Cerrado.

## 6.2.9 — DRY — `CalcularTotales` un único punto de cálculo

**Estado:** ✅ **HECHO**

**Ubicación:** `src/OilsMexico.Infrastructure/Services/CotizacionesService.cs`, líneas 30-34

```csharp
private static (decimal Subtotal, decimal Iva, decimal Total) CalcularTotales(List<CotizacionDetalle> detalles)
{
    decimal subtotal = detalles.Sum(d => CalcularImporte(d.Cantidad, d.PrecioUnitario, d.Descuento));
    return (Math.Round(subtotal, 2), Impuestos.IvaDeBase(subtotal), CalcularTotal(subtotal));
}
```

**Comentario en el código:**
- `CrearCotizacionAsync` línea 57: `// Un solo cálculo de totales (§6.2 — DRY: antes se invocaba CalcularTotales 3 veces).`
- `MapearACotizacionListadoDto` línea 132: `// Un solo cálculo de totales por fila (§6.2 — DRY: antes se invocaba CalcularTotales 3 veces).`

✅ Cerrado.

## 6.2.10 — A9 — `EstadoFactura` enum en lugar de literales aislados

**Estado:** ✅ **HECHO**

**Ubicación:** `src/OilsMexico.Domain/Enums/ErpEnums.cs` — `public enum EstadoFactura`

**Uso verificado:** `ComplementoPagoService.Cobro.cs` usa `EstadoRep.Pendiente` / `EstadoRep.Timbrado` (enums). `FacturacionService.cs`, `NotaCreditoService.*`, `VentasService.Venta.cs` usan `EstadoFactura.Timbrada`, `EstadoFactura.Cancelada`, etc. (enums, no literales string).

✅ Cerrado.

---

## Conclusión

**Toda la sección §6.2 (SOLID / DRY / Mantenibilidad) ya está implementada en el código fuente.** Los checklists anteriores (`Checklist completitud ERP.md`, `por implementar 14 octubre 2026.md`) referenciaban estados de versiones previas del código; las correcciones ya fueron aplicadas.

| Ítem | Estado |
|------|--------|
| 6.2.1 | ✅ Hecho |
| 6.2.2 | ✅ Hecho |
| 6.2.3 | ✅ Hecho |
| 6.2.4 | ✅ Hecho |
| 6.2.5 | ✅ Hecho |
| 6.2.6 | ✅ Hecho |
| 6.2.7 | ✅ Hecho |
| 6.2.8 | ✅ Hecho |
| 6.2.9 | ✅ Hecho |
| 6.2.10 | ✅ Hecho |

## Archivos modificados en esta sesión

**Ninguno.** Todos los ítems de §6.2 ya estaban implementados en el código fuente. Solo se documentó el estado verificado.

## Build

```
dotnet build OilsMexico.slnx --no-restore
→ 0 Errores, 29 Advertencias (todas pre-existing, ninguna nueva)
```

## Siguientes pasos recomendados

- **§6.3 CFDI/PAC modo real:** Colocar CSD, configurar `user-secrets`, probar Finkok TEST (PUE, PPD+REP, cancelación, `EstadoSatAsync`).
- **T-01 a T-05:** Bloqueo por morosidad, verificación CxC/CxP con PostgreSQL, cierre contable con BD real, adjuntar PDF del CFDI al correo, ampliar `AuditLog`.

```

El patrón `ctx.Rol == Roles.Admin ? solicitada : SucursalId` ya no aparece duplicado en los servicios de infraestructura. Los servicios usan `ctx.ResolverSucursalAutorizada(...)`.

✅ Cerrado.

## 6.2.2 — DIP — `Metas.razor.cs` / `VentasPOS.razor.cs` sin `ErpDbContext`

**Estado:** ✅ **HECHO**

**Verificación:**
- `Metas.razor.cs`: inyecta `IAutorizacionService`, `IComisionesService`, `ICatalogosService`, `ISesionActual`, `NavigationManager` — no `ErpDbContext`.
- `VentasPOS.razor.cs`: inyecta `IAutorizacionService`, `IVentasPosService`, `ICatalogosSatService`, `ICatalogosService`, `IClientesService`, `ISucursalContext`, `IHubContext<ErpHub>`, `ISesionActual`, `NavigationManager`, `ICorteCajaService`, `IAvisos` — no `ErpDbContext`.

✅ Cerrado.

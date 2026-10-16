# Deuda técnica al 15 de octubre — §6.1 Hardcode remanente en UI

Estado de cada ítem de la sección **6.1** del `Checklist completitud ERP.md`, verificado contra el código real del repositorio el 15/10/2026.

---

## Resumen

| Ítem | Archivo | Estado | Observación |
|------|---------|--------|-------------|
| 6.1.1 | `Usuarios.razor` | ✅ Hecho | Ya itera `Roles.Todos` + `Roles.Etiqueta()` |
| 6.1.2 | `Compras.razor` | ✅ Hecho | Ya consume `ICatalogosSatService.FormasPago` |
| 6.1.3 | `Facturacion.razor` | ✅ Hecho | Ya usa `CfdiDefaults.MotivosCancelacion` |
| 6.1.3 | `NotasCredito.razor` | ✅ Hecho | Ya usa `CfdiDefaults.MotivosCancelacion` |
| 6.1.4 | `Home.razor` (IVA) | ✅ Hecho | Ya usa `Impuestos.TasaIva * 100` |
| 6.1.5 | `Home.razor` (PAC) | ✅ Hecho | Ya muestra modo PAC real desde configuración |
| 6.1.6 | Decisión arquitectura | ✅ Cerrado | Mantener `CatalogosSatService` en código |

---

## 6.1.1 — `Usuarios.razor` — Roles en combo

**Estado:** ✅ **HECHO** (ya implementado en el código)

**Código actual (líneas 87-92):**
```razor
<select @bind="edit.Rol">
    @foreach (var rol in OilsMexico.Domain.Services.Roles.Todos)
    {
        <option value="@rol">@OilsMexico.Domain.Services.Roles.Etiqueta(rol)</option>
    }
</select>
```

**Fuente única:** `Domain/Services/Roles.cs` — `Roles.Todos` y `Roles.Etiquetas`.

**Conclusión:** No hay hardcode de roles. ✅ Cerrado.

## 6.1.2 — `Compras.razor` — Formas de pago SAT

**Estado:** ✅ **HECHO** (ya implementado)

**Código actual (líneas 270-274):**
```razor
<select @bind="pagoForma">
    @foreach (var kv in catalogosSat.FormasPago)
    {
        <option value="@kv.Key">@kv.Value</option>
    }
</select>
```

**Inyección:** `ICatalogosSatService Sat` → `catalogosSat = Sat.Obtener()` en `OnInitializedAsync`.

**Servicio:** `CatalogosSatService` en `Application/Services/ContextoCatalogos.cs` con diccionarios centralizados.

**Conclusión:** Ya se consume `ICatalogosSatService`. No hay literales hardcodeados. ✅ Cerrado.

## 6.1.3 — `Facturacion.razor` + `NotasCredito.razor` — Motivos de cancelación

**Estado:** ✅ **HECHO** (ya implementado)

Ambas páginas usan:
```razor
@foreach (var m in OilsMexico.Domain.Entities.CfdiDefaults.MotivosCancelacion)
{
    <option value="@m.Codigo">@m.Codigo - @m.Descripcion</option>
}
```

**Fuente única:** `Domain/Entities/CfdiDefaults.cs` — `MotivosCancelacion`.

**Conclusión:** No hay duplicados hardcodeados. ✅ Cerrado.

## 6.1.4 — `Home.razor` — IVA calculado dinámicamente

**Estado:** ✅ **HECHO** (ya implementado)

**Código actual (línea 296):**
```razor
$"IVA {Impuestos.TasaIva * 100:0}% incluido"
```

**Y línea 312:**
```razor
$"{Impuestos.TasaIva * 100:0}% sobre ventas"
```

**Fuente única:** `Domain/Services/Impuestos.cs` — `Impuestos.TasaIva = 0.16m`.

**Conclusión:** Ya se calcula con `Impuestos.TasaIva`. No hay literal "IVA 16%" hardcodeado. ✅ Cerrado.

## 6.1.5 — `Home.razor` — Estado real del PAC

**Estado:** ✅ **HECHO** (ya implementado)

**Código actual (línea 313):**
```razor
lista.Add(new("Estado del PAC", pacModo, $"Modo {pacModo} · Finkok CFDI 4.0", ...));
```

**Definición de `pacModo`:** leído desde `Config["Cfdi:PacModo"] ?? "SIMULADO"`.

**Conclusión:** Ya se muestra el modo PAC real desde configuración. No hay "Módulo futuro (PAC)" hardcodeado. ✅ Cerrado.

## 6.1.6 — Decisión de arquitectura: catálogos SAT en código vs. tabla `catalogos_sat`

**Estado:** ✅ **CERRADO — DECISIÓN TOMADA**

**Situación actual:** `CatalogosSatService` (en `Application/Services/ContextoCatalogos.cs`) contiene los catálogos SAT como diccionarios estáticos en C# (`FormasPago`, `MetodosPago`, `UsosCfdi`, `Regimenes`). Ya están centralizados en un solo servicio.

**Decisión:** Mantener en código. Los catálogos SAT son catálogos oficiales del gobierno mexicano que cambian muy raramente. Ya están centralizados en `CatalogosSatService` accesible vía `ICatalogosSatService.Obtener()`. Moverlos a una tabla `catalogos_sat` añadiría complejidad (migraciones, seed, caché, UI de administración) sin beneficio tangible para catálogos que el negocio no modifica.

Si en el futuro el negocio requiere editarlos desde la UI, se puede migrar a tabla `catalogos_sat` sin romper contratos (la interfaz `ICatalogosSatService` ya abstrae el origen de datos).

✅ **Cerrado con decisión documentada.**

---

## Conclusión

Todos los ítems de §6.1 ya están implementados en el código:

| Ítem | Estado | Evidencia |
|------|--------|-----------|
| 6.1.1 | ✅ | `Usuarios.razor` itera `Roles.Todos` |
| 6.1.2 | ✅ | `Compras.razor` usa `catalogosSat.FormasPago` |
| 6.1.3 | ✅ | `Facturacion` + `NotasCredito` usan `CfdiDefaults.MotivosCancelacion` |
| 6.1.4 | ✅ | `Home.razor` calcula IVA con `Impuestos.TasaIva` |
| 6.1.5 | ✅ | `Home.razor` muestra modo PAC real desde `Config["Cfdi:PacModo"]` |
| 6.1.6 | ✅ | Decisión: mantener `CatalogosSatService` en código |

**La deuda técnica §6.1 está cerrada.** Los checklists anteriores referenciaban números de línea de versiones previas del código; las correcciones ya fueron aplicadas.

## Archivos modificados en esta sesión

Ningún archivo de código fue modificado. Todos los ítems de §6.1 ya estaban implementados en el código fuente. Solo se documentó el estado y se tomó la decisión de arquitectura para 6.1.6.

## Siguientes pasos recomendados (otras secciones de deuda técnica)

- **§6.2 SOLID/DRY:** B8 helper de sucursal, DIP en `Metas.razor.cs`/`VentasPOS.razor.cs`, ISP de `ICotizacionesService`/`IVentasService`, atomicidad de `ConvertirOrdenAVentaAsync`, etc.
- **§6.3 CFDI/PAC modo real:** Colocar CSD, configurar user-secrets, probar Finkok TEST.


using System.Globalization;
using System.Text;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Procesa e importa listas de precios de aceites desde archivos Excel (.xlsx/.xls)
/// mapeando todas las características técnicas, comerciales y presentaciones.
/// </summary>
public sealed class ImportacionProductosService(
    ErpDbContext db,
    ILogger<ImportacionProductosService> logger) : IImportacionProductosService
{
    static ImportacionProductosService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<List<ProductoImportadoFilaDto>> PrevisualizarExcelAsync(Stream stream, CancellationToken ct = default)
    {
        var filas = LeerFilasDeExcel(stream);
        if (filas.Count == 0) return filas;

        var skus = filas.Where(f => !string.IsNullOrWhiteSpace(f.SkuActual))
            .Select(f => f.SkuActual.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var existentes = await db.Productos.AsNoTracking()
            .Where(p => skus.Contains(p.Sku.ToUpper()))
            .Select(p => new { p.Id, Sku = p.Sku.ToUpper() })
            .ToDictionaryAsync(p => p.Sku, p => p.Id, ct);

        foreach (var f in filas)
        {
            var skuNorm = f.SkuActual.Trim().ToUpperInvariant();
            if (existentes.TryGetValue(skuNorm, out var id))
            {
                f.ExisteEnBd = true;
                f.ProductoExistenteId = id;
            }
        }

        return filas;
    }

    public async Task<ImportacionProductosResumenDto> ImportarDesdeArchivoLocalAsync(string rutaArchivo, bool actualizarExistentes = true, CancellationToken ct = default)
    {
        if (!File.Exists(rutaArchivo))
        {
            return new ImportacionProductosResumenDto
            {
                Mensaje = $"El archivo '{rutaArchivo}' no existe en el servidor.",
                Errores = 1,
                ErroresDetalle = [$"Archivo no encontrado: {rutaArchivo}"]
            };
        }

        await using var stream = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await ImportarDesdeStreamAsync(stream, actualizarExistentes, ct);
    }

    public async Task<ImportacionProductosResumenDto> ImportarDesdeStreamAsync(Stream stream, bool actualizarExistentes = true, CancellationToken ct = default)
    {
        var resumen = new ImportacionProductosResumenDto();
        var filas = LeerFilasDeExcel(stream);
        resumen.TotalFilas = filas.Count;

        if (filas.Count == 0)
        {
            resumen.Mensaje = "No se encontraron filas de productos válidas en el archivo Excel.";
            return resumen;
        }

        var sucursalesIds = await db.Sucursales.Select(s => s.Id).ToListAsync(ct);

        // Agrupar o deduplicar filas en el mismo archivo por SKU (si hay duplicados en el Excel, se toma el último o se actualiza)
        var skusEnArchivo = filas.Where(f => f.EsValido && !string.IsNullOrWhiteSpace(f.SkuActual))
            .Select(f => f.SkuActual.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var existentesEnBd = await db.Productos
            .Include(p => p.Unidades)
            .Where(p => skusEnArchivo.Contains(p.Sku.ToUpper()))
            .ToDictionaryAsync(p => p.Sku.ToUpper(), p => p, ct);

        var procesadosSku = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in filas)
        {
            if (!f.EsValido)
            {
                resumen.Errores++;
                if (!string.IsNullOrEmpty(f.ErrorValidacion))
                    resumen.ErroresDetalle.Add($"Fila {f.FilaNumero} ({f.Hoja}): {f.ErrorValidacion}");
                continue;
            }

            var skuNorm = f.SkuActual.Trim().ToUpperInvariant();

            try
            {
                var tipoBase = InferirTipoBase(f.Categoria, f.Descripcion);
                var marca = InferirMarca(f.Descripcion);
                var viscosidad = InferirViscosidad(f.Sae, f.Descripcion);

                if (existentesEnBd.TryGetValue(skuNorm, out var prodExistente))
                {
                    if (!actualizarExistentes)
                    {
                        resumen.Omitidos++;
                        continue;
                    }

                    // Actualizar datos del producto existente
                    prodExistente.Nombre = f.Descripcion;
                    prodExistente.Marca = marca;
                    prodExistente.Viscosidad = viscosidad;
                    prodExistente.TipoBase = tipoBase;
                    prodExistente.Categoria = f.Categoria;
                    prodExistente.SkuAnterior = string.IsNullOrWhiteSpace(f.SkuAnterior) ? prodExistente.SkuAnterior : f.SkuAnterior.Trim();
                    prodExistente.Sae = f.Sae;
                    prodExistente.Especificacion = f.Especificacion;
                    prodExistente.PiezasPorCaja = f.PiezasPorCaja;
                    prodExistente.PrecioLista = f.PrecioLp;
                    prodExistente.PrecioLpOroConIva = f.LpOroConIva;
                    prodExistente.PrecioUnitario = f.PrecioUnitario;
                    prodExistente.PrecioVenta = f.PrecioUnitario > 0 ? f.PrecioUnitario : f.LpOroConIva;
                    if (prodExistente.PrecioMayoreo == 0)
                        prodExistente.PrecioMayoreo = Math.Round(prodExistente.PrecioVenta * 0.90m, 2);
                    prodExistente.Activo = true;

                    AsegurarPresentaciones(prodExistente, f);

                    resumen.Actualizados++;
                }
                else
                {
                    // Alta de nuevo producto
                    var nuevoProd = new Producto
                    {
                        Sku = skuNorm,
                        SkuAnterior = string.IsNullOrWhiteSpace(f.SkuAnterior) ? null : f.SkuAnterior.Trim(),
                        Nombre = f.Descripcion,
                        Marca = marca,
                        Viscosidad = viscosidad,
                        TipoBase = tipoBase,
                        Categoria = f.Categoria,
                        Sae = f.Sae,
                        Especificacion = f.Especificacion,
                        PiezasPorCaja = f.PiezasPorCaja,
                        PrecioLista = f.PrecioLp,
                        PrecioLpOroConIva = f.LpOroConIva,
                        PrecioUnitario = f.PrecioUnitario,
                        PrecioVenta = f.PrecioUnitario > 0 ? f.PrecioUnitario : f.LpOroConIva,
                        PrecioMayoreo = Math.Round((f.PrecioUnitario > 0 ? f.PrecioUnitario : f.LpOroConIva) * 0.90m, 2),
                        PrecioCosto = Math.Round((f.PrecioUnitario > 0 ? f.PrecioUnitario : f.LpOroConIva) * 0.70m, 2),
                        Activo = true
                    };

                    db.Productos.Add(nuevoProd);
                    existentesEnBd[skuNorm] = nuevoProd; // Registrar por si aparece de nuevo en el mismo archivo

                    // Guardar ID provisional para relaciones si es necesario
                    await db.SaveChangesAsync(ct);

                    AsegurarPresentaciones(nuevoProd, f);

                    // Inicializar inventario en cada sucursal si no existe
                    foreach (var sucId in sucursalesIds)
                    {
                        var yaTieneInv = await db.InventarioSucursal
                            .AnyAsync(i => i.SucursalId == sucId && i.ProductoId == nuevoProd.Id, ct);
                        if (!yaTieneInv)
                        {
                            db.InventarioSucursal.Add(new InventarioSucursal
                            {
                                SucursalId = sucId,
                                ProductoId = nuevoProd.Id,
                                LoteId = null,
                                StockActual = 0,
                                StockMinimo = 5
                            });
                        }
                    }

                    resumen.Insertados++;
                }

                procesadosSku.Add(skuNorm);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al importar fila de SKU {Sku}", f.SkuActual);
                resumen.Errores++;
                resumen.ErroresDetalle.Add($"SKU {f.SkuActual}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync(ct);

        resumen.FilasProcesadas = filas;
        resumen.Mensaje = $"Proceso finalizado con éxito: {resumen.Insertados} productos nuevos insertados, {resumen.Actualizados} actualizados, {resumen.Omitidos} omitidos, {resumen.Errores} errores.";
        return resumen;
    }

    private static void AsegurarPresentaciones(Producto prod, ProductoImportadoFilaDto f)
    {
        // 1. Presentación individual (Pieza / Botella / Garrafa / Tambor)
        var nombreUnidadIndiv = DeterminarNombreUnidad(f.Descripcion, f.PiezasPorCaja);
        var factorIndiv = DeterminarFactorLitros(f.Descripcion);
        var precioIndiv = f.PrecioUnitario > 0 ? f.PrecioUnitario : (f.PiezasPorCaja > 0 ? Math.Round(f.LpOroConIva / f.PiezasPorCaja, 2) : f.LpOroConIva);

        var uIndiv = prod.Unidades.FirstOrDefault(u => u.UnidadNombre == nombreUnidadIndiv || u.UnidadNombre == "Litro" || u.UnidadNombre == "Pieza");
        if (uIndiv is null)
        {
            prod.Unidades.Add(new UnidadMedida
            {
                ProductoId = prod.Id,
                UnidadNombre = nombreUnidadIndiv,
                FactorConversion = factorIndiv,
                PrecioUnitario = precioIndiv
            });
        }
        else
        {
            uIndiv.PrecioUnitario = precioIndiv;
            if (factorIndiv > 0) uIndiv.FactorConversion = factorIndiv;
        }

        // 2. Si viene por caja (PiezasPorCaja > 1), asegurar la presentación "Caja"
        if (f.PiezasPorCaja > 1)
        {
            var uCaja = prod.Unidades.FirstOrDefault(u => u.UnidadNombre == "Caja");
            var precioCaja = f.LpOroConIva > 0 ? f.LpOroConIva : f.PrecioLp;
            if (uCaja is null)
            {
                prod.Unidades.Add(new UnidadMedida
                {
                    ProductoId = prod.Id,
                    UnidadNombre = "Caja",
                    FactorConversion = f.PiezasPorCaja * factorIndiv,
                    PrecioUnitario = precioCaja
                });
            }
            else
            {
                uCaja.FactorConversion = f.PiezasPorCaja * factorIndiv;
                uCaja.PrecioUnitario = precioCaja;
            }
        }
    }

    private static string DeterminarNombreUnidad(string desc, int piezasPorCaja)
    {
        var d = desc.ToUpperInvariant();
        if (d.Contains("208L") || d.Contains("208 L") || d.Contains("TAMBOR")) return "Tambor";
        if (d.Contains("18.9L") || d.Contains("19L") || d.Contains("CUBETA")) return "Cubeta";
        if (d.Contains("4.73L") || d.Contains("GARRAFA")) return "Garrafa";
        if (d.Contains("0.946") || d.Contains("BOTELLA") || d.Contains("1L")) return "Botella";
        return piezasPorCaja > 1 ? "Botella" : "Pieza";
    }

    private static decimal DeterminarFactorLitros(string desc)
    {
        var d = desc.ToUpperInvariant();
        if (d.Contains("208L") || d.Contains("208.2L")) return 208m;
        if (d.Contains("18.9L") || d.Contains("19L")) return 18.9m;
        if (d.Contains("4.73L")) return 4.73m;
        if (d.Contains("0.946") || d.Contains("0.976")) return 0.946m;
        return 1m;
    }

    private static string InferirTipoBase(string categoria, string descripcion)
    {
        var cat = (categoria ?? "").ToUpperInvariant();
        var desc = (descripcion ?? "").ToUpperInvariant();

        if (cat.Contains("SEMI")) return "Semisintetico";
        if (cat.Contains("SINTET")) return "Sintetico";
        if (cat.Contains("MINER")) return "Mineral";

        if (desc.Contains("SEMI SINTETICO") || desc.Contains("SEMISINTETICO")) return "Semisintetico";
        if (desc.Contains("SINTETICO") || desc.Contains("SYNTHETIC")) return "Sintetico";
        if (desc.Contains("MINERAL")) return "Mineral";

        if (cat.Contains("MOTO")) return desc.Contains("SINT") ? "Sintetico" : "Mineral";
        if (cat.Contains("TRANSMIS")) return desc.Contains("SINT") ? "Sintetico" : "Mineral";
        if (cat.Contains("HD")) return "Mineral";

        return "Sintetico";
    }

    private static string InferirMarca(string descripcion)
    {
        var d = (descripcion ?? "").ToUpperInvariant();
        if (d.Contains("EDGE") || d.Contains("GTX") || d.Contains("ACTEVO") ||
            d.Contains("POWER 1") || d.Contains("TRANSMAX") || d.Contains("CRB") ||
            d.Contains("AXLE") || d.Contains("GARDEN") || d.Contains("OUTBOARD") ||
            d.Contains("CASTROL"))
        {
            return "Castrol";
        }
        if (d.Contains("MOBIL")) return "Mobil";
        if (d.Contains("VALVOLINE")) return "Valvoline";
        if (d.Contains("MOTUL")) return "Motul";
        if (d.Contains("SHELL") || d.Contains("HELIX") || d.Contains("RIMULA")) return "Shell";
        if (d.Contains("QUAKER")) return "Quaker State";
        if (d.Contains("ROSHFRANS")) return "Roshfrans";
        return "Castrol";
    }

    private static string InferirViscosidad(string sae, string descripcion)
    {
        var s = (sae ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(s) && s != "-") return s;

        var d = (descripcion ?? "").ToUpperInvariant();
        // Buscar patrones como 5W-30, 20W-50, etc.
        var match = System.Text.RegularExpressions.Regex.Match(d, @"\b\d{1,2}W-\d{2}\b");
        if (match.Success) return match.Value;

        if (d.Contains("VISCOSIDAD 40") || d.Contains("SAE 40")) return "40";
        if (d.Contains("80W-90")) return "80W-90";
        if (d.Contains("10W-50")) return "10W-50";

        return !string.IsNullOrWhiteSpace(s) ? s : "N/A";
    }

    private static List<ProductoImportadoFilaDto> LeerFilasDeExcel(Stream stream)
    {
        var lista = new List<ProductoImportadoFilaDto>();
        using var reader = ExcelReaderFactory.CreateReader(stream);

        do
        {
            var hojaNombre = reader.Name ?? "Hoja";
            var indiceColumna = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var filaIndice = 0;

            while (reader.Read())
            {
                filaIndice++;

                if (filaIndice == 1)
                {
                    // Detectar encabezados
                    for (int c = 0; c < reader.FieldCount; c++)
                    {
                        var raw = reader.GetValue(c)?.ToString() ?? "";
                        var h = NormalizarEncabezado(raw);
                        if (string.IsNullOrEmpty(h)) continue;

                        if (h.Contains("CATEGOR")) indiceColumna["categoria"] = c;
                        else if (h.Contains("SKU") && (h.Contains("ANT") || h.Contains("PREV"))) indiceColumna["sku_anterior"] = c;
                        else if (h.Contains("SKU") && (h.Contains("ACT") || h.Contains("NUEV"))) indiceColumna["sku_actual"] = c;
                        else if (h.Contains("SKU") && !indiceColumna.ContainsKey("sku_actual")) indiceColumna["sku_actual"] = c;
                        else if (h.Contains("DESCRIP") || h.Contains("NOMBRE") || h.Contains("RMB")) indiceColumna["descripcion"] = c;
                        else if (h == "SAE" || h.Contains("VISCOSIDAD")) indiceColumna["sae"] = c;
                        else if (h.Contains("ESPEC")) indiceColumna["especificacion"] = c;
                        else if (h.Contains("PIEZA") || h.Contains("PZS") || h.Contains("CAJA")) indiceColumna["piezas"] = c;
                        else if (h.Contains("LP") && h.Contains("ORO")) indiceColumna["precio_oro"] = c;
                        else if (h.Contains("PRECIO LP") || (h.Contains("LP") && !h.Contains("ORO"))) indiceColumna["precio_lp"] = c;
                        else if (h.Contains("UNITARIO") || h.Contains("P/U")) indiceColumna["precio_unitario"] = c;
                    }
                    continue;
                }

                // Fila de datos
                var sku = ObtenerString(reader, indiceColumna, "sku_actual");
                var desc = ObtenerString(reader, indiceColumna, "descripcion");

                // Omitir si la fila está totalmente vacía
                if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(desc)) continue;

                var cat = ObtenerString(reader, indiceColumna, "categoria") ?? "";
                var skuAnt = ObtenerString(reader, indiceColumna, "sku_anterior");
                var sae = ObtenerString(reader, indiceColumna, "sae") ?? "";
                var espec = ObtenerString(reader, indiceColumna, "especificacion") ?? "";
                var piezas = ObtenerInt(reader, indiceColumna, "piezas", defecto: 1);
                var precioLp = ObtenerDecimal(reader, indiceColumna, "precio_lp");
                var precioOro = ObtenerDecimal(reader, indiceColumna, "precio_oro");
                var precioUnit = ObtenerDecimal(reader, indiceColumna, "precio_unitario");

                // Si precio unitario viene en 0 pero hay precio oro y piezas, calcularlo
                if (precioUnit == 0 && precioOro > 0 && piezas > 0)
                {
                    precioUnit = Math.Round(precioOro / piezas, 2);
                }

                var filaDto = new ProductoImportadoFilaDto
                {
                    FilaNumero = filaIndice,
                    Hoja = hojaNombre,
                    Categoria = cat.Trim().ToUpperInvariant(),
                    SkuAnterior = string.IsNullOrWhiteSpace(skuAnt) ? null : skuAnt.Trim().ToUpperInvariant(),
                    SkuActual = (sku ?? "").Trim().ToUpperInvariant(),
                    Descripcion = (desc ?? "").Trim(),
                    Sae = sae.Trim(),
                    Especificacion = espec.Trim(),
                    PiezasPorCaja = piezas > 0 ? piezas : 1,
                    PrecioLp = precioLp,
                    LpOroConIva = precioOro,
                    PrecioUnitario = precioUnit
                };

                if (string.IsNullOrWhiteSpace(filaDto.SkuActual))
                {
                    filaDto.EsValido = false;
                    filaDto.ErrorValidacion = "El SKU es obligatorio.";
                }
                else if (string.IsNullOrWhiteSpace(filaDto.Descripcion))
                {
                    filaDto.EsValido = false;
                    filaDto.ErrorValidacion = "La descripción/nombre del producto es obligatoria.";
                }

                lista.Add(filaDto);
            }
        } while (reader.NextResult());

        return lista;
    }

    private static string NormalizarEncabezado(string raw)
    {
        var text = (raw ?? "").Trim().ToUpperInvariant();
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? ObtenerString(IExcelDataReader reader, Dictionary<string, int> cols, string clave)
    {
        if (!cols.TryGetValue(clave, out var colIdx) || colIdx >= reader.FieldCount) return null;
        var val = reader.GetValue(colIdx);
        return val?.ToString()?.Trim();
    }

    private static int ObtenerInt(IExcelDataReader reader, Dictionary<string, int> cols, string clave, int defecto = 1)
    {
        if (!cols.TryGetValue(clave, out var colIdx) || colIdx >= reader.FieldCount) return defecto;
        var val = reader.GetValue(colIdx);
        if (val is null) return defecto;
        if (val is int i) return i;
        if (val is double d) return (int)Math.Round(d);
        if (int.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return defecto;
    }

    private static decimal ObtenerDecimal(IExcelDataReader reader, Dictionary<string, int> cols, string clave)
    {
        if (!cols.TryGetValue(clave, out var colIdx) || colIdx >= reader.FieldCount) return 0m;
        var val = reader.GetValue(colIdx);
        if (val is null) return 0m;
        if (val is decimal m) return m;
        if (val is double d) return (decimal)d;
        if (val is int i) return i;

        var str = val.ToString()?.Replace("$", "").Replace(",", "").Trim();
        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (decimal.TryParse(str, NumberStyles.Any, new CultureInfo("es-MX"), out var parsedMx)) return parsedMx;
        return 0m;
    }
}

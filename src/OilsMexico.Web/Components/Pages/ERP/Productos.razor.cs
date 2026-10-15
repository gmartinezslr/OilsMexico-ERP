using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Productos : ComponentBase
{
    [Microsoft.AspNetCore.Components.Inject]
    private OilsMexico.Application.Interfaces.IAutorizacionService Autorizacion { get; set; } = default!;
    [Inject] private IImportacionProductosService Importador { get; set; } = default!;
    // DIP: el componente no conoce ErpDbContext; productos, presentaciones y stock
    // se leen/guardan vía ICatalogosService (DIP §6.2).
    [Inject] private ICatalogosService Catalogos { get; set; } = default!;

    private string filtro = "", msg = "";
    private bool err, guardando, guardandoUnidad, soloActivos = true;
    private List<ProductoCatalogoDto> lista = [];
    private ProductoCatalogoDto edit = new();
    private List<UnidadMedidaDto> unidades = [];
    private UnidadMedidaDto unidadEdit = new() { UnidadNombre = "Litro", FactorConversion = 1m };
    private List<StockSucursalDto> stocks = [];

    // Estado del modal de importación Excel
    private bool mostrarModalImportar;
    private bool previsualizando;
    private bool importando;
    private bool actualizarExistentes = true;
    private string archivoNombre = "";
    private byte[]? archivoBytes;
    private List<ProductoImportadoFilaDto> filasPrevisualizadas = [];
    private ImportacionProductosResumenDto? resultadoImportacion;
    private string importMsg = "";
    private bool importErr;

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("productos"))) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        lista = await Catalogos.BuscarProductosAsync(filtro, soloActivos, 250);
        if (edit.Id == 0 && lista.Count > 0) await Editar(lista[0].Id);
        else if (edit.Id != 0) await CargarDetalle(edit.Id);
    }

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }

    private void Nuevo()
    {
        edit = new ProductoCatalogoDto
        {
            TipoBase = "Sintetico",
            Categoria = "SINTETICOS",
            PiezasPorCaja = 1,
            Activo = true
        };
        unidades = [];
        unidadEdit = new UnidadMedidaDto { UnidadNombre = "Litro", FactorConversion = 1m };
        stocks = [];
        msg = "";
    }

    private async Task Editar(int id)
    {
        var p = await Catalogos.ObtenerProductoAsync(id);
        if (p is null) return;
        edit = p with { };
        await CargarDetalle(id);
    }

    private async Task CargarDetalle(int productoId)
    {
        unidades = await Catalogos.ListarUnidadesAsync(productoId);
        unidadEdit = new UnidadMedidaDto { ProductoId = productoId, UnidadNombre = "Litro", FactorConversion = 1m, PrecioUnitario = edit.PrecioVenta };
        stocks = await Catalogos.StockPorSucursalAsync(productoId);
    }

    private async Task Guardar()
    {
        msg = "";
        edit.Sku = (edit.Sku ?? "").Trim().ToUpperInvariant();
        edit.Nombre = (edit.Nombre ?? "").Trim();
        edit.Marca = (edit.Marca ?? "").Trim();
        edit.Viscosidad = (edit.Viscosidad ?? "").Trim();
        edit.SkuAnterior = string.IsNullOrWhiteSpace(edit.SkuAnterior) ? null : edit.SkuAnterior.Trim().ToUpperInvariant();
        edit.Categoria = string.IsNullOrWhiteSpace(edit.Categoria) ? null : edit.Categoria.Trim().ToUpperInvariant();
        edit.Sae = string.IsNullOrWhiteSpace(edit.Sae) ? null : edit.Sae.Trim();
        edit.Especificacion = string.IsNullOrWhiteSpace(edit.Especificacion) ? null : edit.Especificacion.Trim();

        if (string.IsNullOrWhiteSpace(edit.Sku) || string.IsNullOrWhiteSpace(edit.Nombre)
            || string.IsNullOrWhiteSpace(edit.Marca) || string.IsNullOrWhiteSpace(edit.Viscosidad))
        { msg = "SKU, nombre, marca y viscosidad son obligatorios."; err = true; return; }
        if (string.IsNullOrWhiteSpace(edit.TipoBase)) edit.TipoBase = "Sintetico";
        if (edit.PiezasPorCaja <= 0) edit.PiezasPorCaja = 1;

        if (edit.PrecioVenta < 0 || edit.PrecioMayoreo < 0 || edit.PrecioUnitario < 0)
        { msg = "Los precios no pueden ser negativos."; err = true; return; }

        if (edit.PrecioVenta == 0 && edit.PrecioUnitario > 0)
            edit.PrecioVenta = edit.PrecioUnitario;
        else if (edit.PrecioUnitario == 0 && edit.PrecioVenta > 0)
            edit.PrecioUnitario = edit.PrecioVenta;

        if (edit.PrecioVenta == 0 && edit.PrecioMayoreo == 0 && edit.PrecioLista == 0)
        { msg = "Captura al menos un precio mayor a cero."; err = true; return; }

        guardando = true;
        try
        {
            // Alta/edición + duplicado de SKU + stock en sucursales y presentación base
            // viven en el servicio; aquí sólo se muestra el resultado.
            var (ok, mensaje, id) = await Catalogos.GuardarProductoAsync(edit);
            msg = mensaje; err = !ok;
            if (ok)
            {
                await Cargar();
                await Editar(id);
            }
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Desactivar()
    {
        var (ok, mensaje) = await Catalogos.CambiarEstadoProductoAsync(edit.Id, false);
        msg = ok ? "Producto desactivado (ya no aparece en POS)." : mensaje; err = !ok;
        if (!ok) return;
        await Cargar();
        await Editar(edit.Id);
    }

    private async Task Reactivar()
    {
        var (ok, mensaje) = await Catalogos.CambiarEstadoProductoAsync(edit.Id, true);
        msg = ok ? "Producto reactivado." : mensaje; err = !ok;
        if (!ok) return;
        await Cargar();
        await Editar(edit.Id);
    }

    private void NuevaUnidad()
    {
        unidadEdit = new UnidadMedidaDto
        { ProductoId = edit.Id, UnidadNombre = "Litro", FactorConversion = 1m, PrecioUnitario = edit.PrecioVenta };
    }

    private async Task EditarUnidad(int id)
    {
        var u = await Catalogos.ObtenerUnidadAsync(id);
        if (u is null) return;
        unidadEdit = u with { };
    }

    private async Task GuardarUnidad()
    {
        msg = "";
        if (edit.Id == 0) { msg = "Guarda primero el producto."; err = true; return; }
        guardandoUnidad = true;
        try
        {
            // Validaciones (unidad, factor, precio, código de barras y duplicados) en el servicio.
            unidadEdit.ProductoId = edit.Id;
            var (ok, mensaje, _) = await Catalogos.GuardarUnidadAsync(unidadEdit);
            msg = mensaje; err = !ok;
            if (ok) await CargarDetalle(edit.Id);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardandoUnidad = false;
    }

    private async Task EliminarUnidad(int id)
    {
        var (ok, mensaje) = await Catalogos.EliminarUnidadAsync(id);
        msg = mensaje; err = !ok;
        if (ok) await CargarDetalle(edit.Id);
    }

    // ==========================================
    // METODOS DEL PROCESO DE IMPORTACION EXCEL
    // ==========================================

    private void AbrirModalImportacion()
    {
        mostrarModalImportar = true;
        filasPrevisualizadas = [];
        resultadoImportacion = null;
        archivoNombre = "";
        archivoBytes = null;
        importMsg = "";
        importErr = false;
    }

    private void CerrarModalImportacion()
    {
        mostrarModalImportar = false;
    }

    private async Task CargarArchivoDelSistemaAsync()
    {
        importMsg = "";
        importErr = false;
        previsualizando = true;
        resultadoImportacion = null;

        try
        {
            // Buscar lista_precios_aceites.xlsx en directorios comunes
            var posiblesRutas = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "lista_precios_aceites.xlsx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lista_precios_aceites.xlsx"),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "lista_precios_aceites.xlsx")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "lista_precios_aceites.xlsx")),
                @"D:\GMS\Jerry\OilsMexico\lista_precios_aceites.xlsx"
            };

            var ruta = posiblesRutas.FirstOrDefault(File.Exists);
            if (ruta is null)
            {
                importMsg = "No se encontró el archivo 'lista_precios_aceites.xlsx' en el servidor. Puedes seleccionarlo manualmente con el botón 'Examinar'.";
                importErr = true;
                return;
            }

            archivoNombre = Path.GetFileName(ruta);
            await using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using var msCopy = new MemoryStream();
                await fs.CopyToAsync(msCopy);
                archivoBytes = msCopy.ToArray();
            }

            using var ms = new MemoryStream(archivoBytes);
            filasPrevisualizadas = await Importador.PrevisualizarExcelAsync(ms);

            var nuevos = filasPrevisualizadas.Count(f => !f.ExisteEnBd);
            var existentes = filasPrevisualizadas.Count(f => f.ExisteEnBd);
            importMsg = $"Archivo '{archivoNombre}' cargado con éxito. Se detectaron {filasPrevisualizadas.Count} productos ({nuevos} nuevos, {existentes} existentes).";
            importErr = false;
        }
        catch (Exception ex)
        {
            importMsg = "Error al leer el archivo del sistema: " + ex.Message;
            importErr = true;
        }
        finally
        {
            previsualizando = false;
        }
    }

    private async Task OnArchivoSeleccionadoAsync(InputFileChangeEventArgs e)
    {
        importMsg = "";
        importErr = false;
        previsualizando = true;
        resultadoImportacion = null;

        try
        {
            var archivo = e.File;
            if (archivo is null) return;

            archivoNombre = archivo.Name;
            using var stream = archivo.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            archivoBytes = ms.ToArray();

            ms.Position = 0;
            filasPrevisualizadas = await Importador.PrevisualizarExcelAsync(ms);

            var nuevos = filasPrevisualizadas.Count(f => !f.ExisteEnBd);
            var existentes = filasPrevisualizadas.Count(f => f.ExisteEnBd);
            importMsg = $"Archivo '{archivoNombre}' analizado. Se detectaron {filasPrevisualizadas.Count} productos ({nuevos} nuevos, {existentes} existentes en el catálogo).";
            importErr = false;
        }
        catch (Exception ex)
        {
            importMsg = "Error al procesar el archivo Excel seleccionado: " + ex.Message;
            importErr = true;
        }
        finally
        {
            previsualizando = false;
        }
    }

    private async Task EjecutarImportacionAsync()
    {
        if (archivoBytes is null || archivoBytes.Length == 0)
        {
            importMsg = "Por favor carga un archivo Excel primero.";
            importErr = true;
            return;
        }

        importando = true;
        importMsg = "";
        try
        {
            using var ms = new MemoryStream(archivoBytes);
            resultadoImportacion = await Importador.ImportarDesdeStreamAsync(ms, actualizarExistentes);

            importMsg = resultadoImportacion.Mensaje;
            importErr = resultadoImportacion.Errores > 0 && resultadoImportacion.Insertados == 0 && resultadoImportacion.Actualizados == 0;

            // Recargar catálogo de productos
            await Cargar();
        }
        catch (Exception ex)
        {
            importMsg = "Error durante la importación: " + ex.Message;
            importErr = true;
        }
        finally
        {
            importando = false;
        }
    }
}

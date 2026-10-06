using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Productos : ComponentBase
{
    private string filtro = "", msg = "";
    private bool err, guardando, guardandoUnidad, soloActivos = true;
    private List<Producto> lista = [];
    private Producto edit = new() { TipoBase = "Sintetico" };
    private List<UnidadMedida> unidades = [];
    private UnidadMedida unidadEdit = new() { UnidadNombre = "Litro", FactorConversion = 1m };
    private List<StockRow> stocks = [];

    private sealed record StockRow(string Sucursal, decimal Stock, decimal Minimo, bool BajoMinimo);

    protected override async Task OnInitializedAsync() => await Cargar();

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        var q = Db.Productos.AsNoTracking().OrderBy(p => p.Nombre).AsQueryable();
        if (soloActivos) q = q.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(p => p.Sku.ToLower().Contains(f) || p.Nombre.ToLower().Contains(f)
                || p.Marca.ToLower().Contains(f) || p.Viscosidad.ToLower().Contains(f));
        }
        lista = await q.Take(200).ToListAsync();
        if (edit.Id == 0 && lista.Count > 0) await Editar(lista[0].Id);
        else if (edit.Id != 0) await CargarDetalle(edit.Id);
    }

    private async Task AlEscribir(ChangeEventArgs e) { filtro = e.Value?.ToString() ?? ""; await Cargar(); }

    private void Nuevo()
    {
        edit = new Producto { TipoBase = "Sintetico", Activo = true };
        unidades = [];
        unidadEdit = new UnidadMedida { UnidadNombre = "Litro", FactorConversion = 1m };
        stocks = [];
        msg = "";
    }

    private async Task Editar(int id)
    {
        var p = await Db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return;
        edit = Clonar(p);
        await CargarDetalle(id);
    }
    private async Task CargarDetalle(int productoId)
    {
        unidades = await Db.UnidadesMedida.AsNoTracking()
            .Where(u => u.ProductoId == productoId).OrderBy(u => u.Id).ToListAsync();
        unidadEdit = new UnidadMedida { ProductoId = productoId, UnidadNombre = "Litro", FactorConversion = 1m, PrecioUnitario = edit.PrecioVenta };
        var rows = await (from i in Db.InventarioSucursal.AsNoTracking()
                          join s in Db.Sucursales.AsNoTracking() on i.SucursalId equals s.Id
                          where i.ProductoId == productoId
                          group new { i, s } by s.Nombre into g
                          select new { Suc = g.Key, Stock = g.Sum(x => x.i.StockActual), Min = g.Sum(x => x.i.StockMinimo) })
            .ToListAsync();
        stocks = rows.Select(r => new StockRow(r.Suc, r.Stock, r.Min, r.Min > 0 && r.Stock <= r.Min)).ToList();
    }

    private async Task Guardar()
    {
        msg = "";
        edit.Sku = (edit.Sku ?? "").Trim().ToUpperInvariant();
        edit.Nombre = (edit.Nombre ?? "").Trim();
        edit.Marca = (edit.Marca ?? "").Trim();
        edit.Viscosidad = (edit.Viscosidad ?? "").Trim();
        if (string.IsNullOrWhiteSpace(edit.Sku) || string.IsNullOrWhiteSpace(edit.Nombre)
            || string.IsNullOrWhiteSpace(edit.Marca) || string.IsNullOrWhiteSpace(edit.Viscosidad))
        { msg = "SKU, nombre, marca y viscosidad son obligatorios."; err = true; return; }
        if (string.IsNullOrWhiteSpace(edit.TipoBase)) edit.TipoBase = "Sintetico";
        if (edit.PrecioVenta < 0 || edit.PrecioMayoreo < 0)
        { msg = "Los precios no pueden ser negativos."; err = true; return; }
        if (edit.PrecioVenta == 0 && edit.PrecioMayoreo == 0)
        { msg = "Captura al menos un precio mayor a cero."; err = true; return; }
        var dup = await Db.Productos.AsNoTracking()
            .AnyAsync(p => p.Sku.ToLower() == edit.Sku.ToLower() && p.Id != edit.Id);
        if (dup) { msg = "Ya existe otro producto con ese SKU."; err = true; return; }
        guardando = true;
        try
        {
            if (edit.Id == 0)
            {
                Db.Productos.Add(edit);
                await Db.SaveChangesAsync();
                var sucs = await Db.Sucursales.Select(s => s.Id).ToListAsync();
                foreach (var s in sucs)
                    Db.InventarioSucursal.Add(new InventarioSucursal
                    { SucursalId = s, ProductoId = edit.Id, LoteId = null, StockActual = 0, StockMinimo = 5 });
                var precioBase = edit.PrecioVenta > 0 ? edit.PrecioVenta : edit.PrecioMayoreo;
                Db.UnidadesMedida.Add(new UnidadMedida
                { ProductoId = edit.Id, UnidadNombre = "Litro", FactorConversion = 1m, PrecioUnitario = precioBase });
                await Db.SaveChangesAsync();
                msg = "Producto creado (con Litro x1 y stock en sucursales).";
            }
            else
            {
                Db.Productos.Update(edit);
                await Db.SaveChangesAsync();
                msg = "Producto guardado.";
            }
            err = false;
            var id = edit.Id;
            await Cargar();
            await Editar(id);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }
    private async Task Desactivar()
    {
        edit.Activo = false;
        Db.Productos.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Producto desactivado (ya no aparece en POS)."; err = false;
        await Cargar();
        await Editar(edit.Id);
    }

    private async Task Reactivar()
    {
        edit.Activo = true;
        Db.Productos.Update(edit);
        await Db.SaveChangesAsync();
        msg = "Producto reactivado."; err = false;
        await Cargar();
        await Editar(edit.Id);
    }

    private void NuevaUnidad()
    {
        unidadEdit = new UnidadMedida
        { ProductoId = edit.Id, UnidadNombre = "Litro", FactorConversion = 1m, PrecioUnitario = edit.PrecioVenta };
    }

    private async Task EditarUnidad(int id)
    {
        var u = await Db.UnidadesMedida.FindAsync(id);
        if (u is null) return;
        unidadEdit = new UnidadMedida
        { Id = u.Id, ProductoId = u.ProductoId, UnidadNombre = u.UnidadNombre, FactorConversion = u.FactorConversion, CodigoBarra = u.CodigoBarra, PrecioUnitario = u.PrecioUnitario };
    }

    private async Task GuardarUnidad()
    {
        msg = "";
        if (edit.Id == 0) { msg = "Guarda primero el producto."; err = true; return; }
        if (string.IsNullOrWhiteSpace(unidadEdit.UnidadNombre))
        { msg = "La unidad es obligatoria."; err = true; return; }
        if (unidadEdit.FactorConversion <= 0)
        { msg = "Litros por unidad debe ser mayor a cero."; err = true; return; }
        if (unidadEdit.PrecioUnitario < 0)
        { msg = "El precio no puede ser negativo."; err = true; return; }
        unidadEdit.ProductoId = edit.Id;
        unidadEdit.CodigoBarra = string.IsNullOrWhiteSpace(unidadEdit.CodigoBarra) ? null : unidadEdit.CodigoBarra.Trim();
        if (!string.IsNullOrEmpty(unidadEdit.CodigoBarra))
        {
            var dupBar = await Db.UnidadesMedida.AsNoTracking().AnyAsync(u =>
                u.CodigoBarra == unidadEdit.CodigoBarra && u.Id != unidadEdit.Id);
            if (dupBar) { msg = "Ese codigo de barras ya esta en uso."; err = true; return; }
        }
        guardandoUnidad = true;
        try
        {
            if (unidadEdit.Id == 0) Db.UnidadesMedida.Add(unidadEdit);
            else Db.UnidadesMedida.Update(unidadEdit);
            await Db.SaveChangesAsync();
            msg = "Presentacion guardada."; err = false;
            await CargarDetalle(edit.Id);
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardandoUnidad = false;
    }

    private async Task EliminarUnidad(int id)
    {
        var u = await Db.UnidadesMedida.FindAsync(id);
        if (u is null) return;
        if (unidades.Count <= 1)
        { msg = "El producto debe tener al menos una presentacion."; err = true; return; }
        var enVentas = await Db.Set<FacturaDetalle>().AsNoTracking().AnyAsync(d => d.UnidadMedidaId == id);
        if (enVentas) { msg = "No se puede quitar: ya se uso en ventas."; err = true; return; }
        Db.UnidadesMedida.Remove(u);
        await Db.SaveChangesAsync();
        msg = "Presentacion eliminada."; err = false;
        await CargarDetalle(edit.Id);
    }

    private static Producto Clonar(Producto p) => new()
    {
        Id = p.Id, Sku = p.Sku, Nombre = p.Nombre, Marca = p.Marca, Viscosidad = p.Viscosidad,
        TipoBase = p.TipoBase, DescripcionTecnica = p.DescripcionTecnica,
        PrecioVenta = p.PrecioVenta, PrecioMayoreo = p.PrecioMayoreo, Activo = p.Activo
    };
}


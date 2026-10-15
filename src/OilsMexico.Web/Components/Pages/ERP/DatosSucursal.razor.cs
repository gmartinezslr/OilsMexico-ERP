using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class DatosSucursal : ComponentBase
{
    [Inject] private IAutorizacionService Autorizacion { get; set; } = default!;
    // DIP: el componente no conoce ErpDbContext; el catálogo vive en ICatalogosService.
    [Inject] private ICatalogosService Catalogos { get; set; } = default!;
    private string msg = "";
    private bool err, guardando, esAdmin;
    private List<SucursalDto> lista = [];
    private SucursalDto edit = new();
    private CatalogosSatDto catalogos = new([], [], [], []);

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("sucursal"))) { Nav.NavigateTo("/"); return; }
        catalogos = Sat.Obtener();
        esAdmin = Sesion.Sesion?.Rol == Roles.Admin;
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        lista = await Catalogos.ListarSucursalesAsync();
        if (edit.Id == 0 && lista.Count > 0)
        {
            var mia = lista.FirstOrDefault(s => s.Id == SucCtx.SucursalId);
            edit = (mia ?? lista[0]) with { };
        }
    }

    private async Task Editar(int id)
    {
        var s = await Catalogos.ObtenerSucursalAsync(id);
        if (s is not null) edit = s with { };
        msg = "";
    }

    private async Task Guardar()
    {
        msg = "";
        if (!esAdmin) { msg = "Solo el rol Admin puede guardar cambios."; err = true; return; }

        guardando = true;
        try
        {
            // Validaciones de contenido (nombre, código, RFC, CP, colonia) y duplicados
            // viven en el servicio; aquí sólo se muestra el resultado.
            var (ok, mensaje, id) = await Catalogos.GuardarSucursalAsync(edit);
            msg = mensaje; err = !ok;
            if (ok)
            {
                await Cargar();
                edit = (await Catalogos.ObtenerSucursalAsync(id))! with { };
            }
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }
}

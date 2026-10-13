using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class OrdenesVenta : ComponentBase
{
    private List<OrdenVentaListadoDto> lista = [];
    private string filtroTexto = "";
    private EstadoOrdenVenta? filtroEstado;
    private bool cargando;
    private string msg = "";
    private bool err;

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        await Cargar();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            var ahora = DateTime.UtcNow;
            lista = await CotizSvc.ListarOrdenesVentaAsync(SucCtx.SucursalId, filtroEstado, filtroTexto,
                null, ahora, 1, 50);
            msg = "";
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        cargando = false;
    }

    private void AlEscribir(ChangeEventArgs e)
    {
        filtroTexto = e.Value?.ToString() ?? "";
        filtroEstado = string.IsNullOrWhiteSpace(e.Value?.ToString()) ? (EstadoOrdenVenta?)null : filtroEstado;
        _ = Cargar();
    }

    private void NuevaOrden() => Nav.NavigateTo("/ordenes-vente/nueva", forceLoad: true);

    private void AbrirDetalle(int id) => Nav.NavigateTo($"/ordenes-vente/detalle/{id}", forceLoad: true);

    private void Cancelar(int id) => Nav.NavigateTo($"/ordenes-vente/detalle/{id}?accion=cancelar", forceLoad: true);

    private static string nombreEstado(EstadoOrdenVenta e) => e switch
    {
        EstadoOrdenVenta.Borrador => "Borrador",
        EstadoOrdenVenta.Confirmada => "Confirmada",
        EstadoOrdenVenta.Suspendida => "Suspendida",
        EstadoOrdenVenta.Cancelada => "Cancelada",
        _ => e.ToString()
    };
}

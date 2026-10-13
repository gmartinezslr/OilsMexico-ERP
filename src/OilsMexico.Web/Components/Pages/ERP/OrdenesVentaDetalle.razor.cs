using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class OrdenesVentaDetalle : ComponentBase
{
    private string? Accion { get; set; }
    [Parameter] public int Id { get; set; }
    private OrdenVentaDetalleDto? orden;
    private List<(int Id, string Nombre)> vendedores = [];
    private int vendedorConf;
    private string motivoCancel = "";
    private bool confirmando;
    private bool cancelando;
    private bool cargando;
    private string msg = "";
    private bool err;

    [Inject] public ICotizacionesService CotizSvc { get; set; } = default!;
    [Inject] public ISucursalContext SucCtx { get; set; } = default!;
    [Inject] public ISesionActual Sesion { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;

        // Parse ?accion=... from the query string (parte tras '?').
        var q = Nav.Uri.Contains('?') ? Nav.Uri[(Nav.Uri.IndexOf('?') + 1)..] : string.Empty;
        Accion = System.Web.HttpUtility.ParseQueryString(q)["accion"];

        await Cargar();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            orden = await CotizSvc.ObtenerOrdenVentaAsync(Id);
            if (orden is null) return;
            vendedores = await CotizSvc.ListarVendedoresAsync(SucCtx.SucursalId);
            vendedorConf = orden.VendedorId ?? 0;
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            cargando = false;
        }
    }

    private void AbrirConfirmar() => Nav.NavigateTo($"/ordenes-vente/detalle/{Id}?accion=confirmar", forceLoad: true);

    private void AbrirCancelar() => Nav.NavigateTo($"/ordenes-vente/detalle/{Id}?accion=cancelar", forceLoad: true);

    private async Task ConfirmarConfirmacion()
    {
        confirmando = true;
        try
        {
            var res = await CotizSvc.ConfirmarOrdenVentaAsync(Id, new OrdenVentaConfirmarRequest(
                vendedorConf > 0 ? vendedorConf : (int?)null, "Confirmado por vendedor del módulo Cotizaciones"));
            msg = $"Orden {res.FolioInterno} confirmada.";
            await Cargar();
            Nav.NavigateTo($"/ordenes-vente/detalle/{res.Id}", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            confirmando = false;
        }
    }

    private async Task ConfirmarCancelacion()
    {
        cancelando = true;
        try
        {
            await CotizSvc.CancelarOrdenVentaAsync(Id, motivoCancel);
            msg = "Orden de venta cancelada.";
            await Cargar();
            Nav.NavigateTo("/ordenes-vente", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            cancelando = false;
        }
    }

    private static string ClaseEstado(EstadoOrdenVenta e) => e switch
    {
        EstadoOrdenVenta.Borrador => "secondary",
        EstadoOrdenVenta.Confirmada => "success",
        EstadoOrdenVenta.Suspendida => "warning",
        EstadoOrdenVenta.Cancelada => "danger",
        _ => "secondary"
    };
}

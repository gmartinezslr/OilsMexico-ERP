using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Services;
using OilsMexico.Domain.Entities;
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
    // Suspensión / reanudación (Capa C).
    private string motivoSusp = "";
    private bool suspendiendo;
    private bool reanudando;
    private bool cargando;
    private string msg = "";
    private bool err;

    // Facturación (Capa B): catálogos SAT y valores por defecto de CfdiDefaults.
    private CatalogosSatDto catalogos = new([], [], [], []);
    private string formaPago = CfdiDefaults.FormaPagoVenta;
    private string metodoPago = CfdiDefaults.MetodoPago;
    private string usoCfdi = CfdiDefaults.UsoCfdiVenta;
    private bool requiereFactura;
    private bool facturando;

    [Inject] public ICotizacionesService CotizSvc { get; set; } = default!;
    [Inject] public IAutorizacionService Autorizacion { get; set; } = default!;
    [Inject] public ICatalogosSatService Sat { get; set; } = default!;
    [Inject] public ISucursalContext SucCtx { get; set; } = default!;
    [Inject] public ISesionActual Sesion { get; set; } = default!;
    [Inject] public NavigationManager Nav { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync())
        {
            Nav.NavigateTo("/login");
            return;
        }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("cotizaciones")))
        {
            Nav.NavigateTo("/");
            return;
        }

        // Parse ?accion=... from the query string (parte tras '?').
        var q = Nav.Uri.Contains('?') ? Nav.Uri[(Nav.Uri.IndexOf('?') + 1)..] : string.Empty;
        Accion = System.Web.HttpUtility.ParseQueryString(q)["accion"];

        await Cargar();
        StateHasChanged();
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
            catalogos = Sat.Obtener();
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

    private void AbrirFacturar() => Nav.NavigateTo($"/ordenes-vente/detalle/{Id}?accion=facturar", forceLoad: true);

    private void AbrirSuspender() => Nav.NavigateTo($"/ordenes-vente/detalle/{Id}?accion=suspender", forceLoad: true);

    private void AbrirReanudar() => Nav.NavigateTo($"/ordenes-vente/detalle/{Id}?accion=reanudar", forceLoad: true);

    /// <summary>Suspende la orden Confirmada (Capa C) y recarga el detalle.</summary>
    private async Task ConfirmarSuspension()
    {
        suspendiendo = true;
        try
        {
            await CotizSvc.SuspenderOrdenVentaAsync(Id, motivoSusp);
            Nav.NavigateTo($"/ordenes-vente/detalle/{Id}", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            suspendiendo = false;
        }
    }

    /// <summary>Reanuda la orden Suspendida (Capa C), que regresa a Confirmada.</summary>
    private async Task ConfirmarReanudacion()
    {
        reanudando = true;
        try
        {
            await CotizSvc.ReanudarOrdenVentaAsync(Id, motivoSusp);
            Nav.NavigateTo($"/ordenes-vente/detalle/{Id}", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            reanudando = false;
        }
    }

    /// <summary>Genera la venta/CFDI desde la orden Confirmada y muestra el detalle ya facturado.</summary>
    private async Task ConfirmarFacturacion()
    {
        facturando = true;
        try
        {
            var res = await CotizSvc.ConvertirOrdenAVentaAsync(Id, new OrdenVentaFacturarRequest(
                formaPago, metodoPago, usoCfdi, requiereFactura));
            // La orden queda Facturada + ligada a la factura; forceLoad limpia ?accion=facturar.
            Nav.NavigateTo($"/ordenes-vente/detalle/{res.OrdenVentaId}", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            facturando = false;
        }
    }

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
        EstadoOrdenVenta.Facturada => "primary",
        _ => "secondary"
    };
}

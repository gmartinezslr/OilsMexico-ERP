using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class CotizacionDetalle : ComponentBase
{
    private string? Accion { get; set; }
    [Parameter] public int Id { get; set; }
    private CotizacionResponseDto? cotizacion;
    private List<(int Id, string Nombre)> vendedores = [];
    private int vendedorConv;
    private string folioConv = "";
    private string motivoCancel = "";
    private bool convenciendo;
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
            cotizacion = await CotizSvc.ObtenerCotizacionAsync(Id);
            if (cotizacion is null) return;
            vendedores = await CotizSvc.ListarVendedoresAsync(SucCtx.SucursalId);
            vendedorConv = cotizacion.VendedorId ?? 0;
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

    private void AbrirConvertir() => Nav.NavigateTo($"/cotizaciones/detalle/{Id}?accion=convertir", forceLoad: true);

    private void AbrirCancelar() => Nav.NavigateTo($"/cotizaciones/detalle/{Id}?accion=cancelar", forceLoad: true);

    private async Task ConfirmarConversion()
    {
        convenciendo = true;
        try
        {
            var res = await CotizSvc.ConvertirCotizacionAsync(Id, new CotizacionConvertirRequest(
                vendedorConv > 0 ? vendedorConv : (int?)null, folioConv, "Convertido por vendedor del módulo Cotizaciones"));
            msg = $"Orden de venta creada: {res.FolioInterno} (total ${res.Total:N2}).";
            await Cargar();
            Nav.NavigateTo($"/ordenes-vente/detalle/{res.OrdenVentaId}", forceLoad: true);
        }
        catch (Exception ex)
        {
            msg = "Error: " + ex.Message;
            err = true;
        }
        finally
        {
            convenciendo = false;
        }
    }

    private async Task ConfirmarCancelacion()
    {
        cancelando = true;
        try
        {
            await CotizSvc.CancelarCotizacionAsync(Id, motivoCancel);
            msg = "Cotización cancelada.";
            await Cargar();
            Nav.NavigateTo("/cotizaciones", forceLoad: true);
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

    private static string ClaseEstado(EstadoCotizacion e) => e switch
    {
        EstadoCotizacion.Borrador => "secondary",
        EstadoCotizacion.Expirada => "warning",
        EstadoCotizacion.Convertida => "success",
        EstadoCotizacion.Cancelada => "danger",
        _ => "secondary"
    };
}

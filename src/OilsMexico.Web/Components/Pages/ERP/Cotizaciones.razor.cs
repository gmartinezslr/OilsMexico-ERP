using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Cotizaciones : ComponentBase
{
    private List<CotizacionListadoDto> lista = [];
    private string filtroTexto = "";
    private EstadoCotizacion? filtroEstado;
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
            lista = await CotizSvc.ListarCotizacionesAsync(SucCtx.SucursalId, filtroEstado, filtroTexto,
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
        filtroEstado = string.IsNullOrWhiteSpace(e.Value?.ToString()) ? (EstadoCotizacion?)null : filtroEstado;
        _ = Cargar();
    }

    private void NuevaCotizacion() => Nav.NavigateTo("/cotizaciones/nueva", forceLoad: true);

    private void AbrirDetalle(int id) => Nav.NavigateTo($"/cotizaciones/detalle/{id}", forceLoad: true);

    private void Convertir(int id) => Nav.NavigateTo($"/cotizaciones/detalle/{id}?accion=convertir", forceLoad: true);

    private void Cancelar(int id) => Nav.NavigateTo($"/cotizaciones/detalle/{id}?accion=cancelar", forceLoad: true);

    private static string nombreEstado(EstadoCotizacion e) => e switch
    {
        EstadoCotizacion.Borrador => "Borrador",
        EstadoCotizacion.Expirada => "Expirada",
        EstadoCotizacion.Convertida => "Convertida",
        EstadoCotizacion.Cancelada => "Cancelada",
        _ => e.ToString()
    };
}

using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;

namespace OilsMexico.Web.Components.Pages.ERP;

// Inyecciones: ver @inject en HistorialVentas.razor (mismo patrón que Clientes/Almacen).
public partial class HistorialVentas : ComponentBase
{
    private DateTime desde = DateTime.Today.AddDays(-30);
    private DateTime hasta = DateTime.Today;
    private string texto = "", estado = "", msg = "";
    private bool err, cargando, devolviendo;
    private string motivoDevolucion = "";
    private int pagina = 1;
    private HistorialResultadoDto resultado = new([], new(0, 0m, 0m, 0m), 0, 1, 1);
    private HistorialDetalleDto? detalle;
    private int? detalleId;

    protected override async Task OnInitializedAsync() { if (Sesion.Autenticado) await Cargar(); }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor" or "Conta")) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            resultado = await Ventas.HistorialAsync(SucCtx.SucursalId, desde, hasta, texto, estado, pagina);
            detalle = null; detalleId = null; devolviendo = false;
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cargando = false;
    }

    private async Task Buscar() { pagina = 1; await Cargar(); }

    private void AlEscribir(ChangeEventArgs e) { texto = e.Value?.ToString() ?? ""; }

    private async Task Expandir(int facturaId)
    {
        if (detalleId == facturaId)
        {
            detalleId = null; detalle = null; devolviendo = false;
            return;
        }
        detalleId = facturaId;
        detalle = null;
        devolviendo = false;
        detalle = await Ventas.HistorialDetalleAsync(facturaId);
        if (detalle is null) { msg = "Venta no encontrada (o es de otra sucursal)."; err = true; detalleId = null; }
    }

    private async Task Surtir(int facturaId)
    {
        try
        {
            var r = await Ventas.SurtirAsync(facturaId);
            msg = $"Venta {r.FolioInterno} surtida."; err = false;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private void EmpezarDevolucion() { devolviendo = true; motivoDevolucion = ""; }

    private async Task ConfirmarDevolucion(int facturaId)
    {
        if (string.IsNullOrWhiteSpace(motivoDevolucion))
        { msg = "Indica el motivo de la devolución."; err = true; return; }
        try
        {
            var r = await Ventas.DevolverAsync(facturaId, motivoDevolucion.Trim());
            msg = $"Devolución registrada para {r.FolioInterno} (stock repuesto)."; err = false;
            await Cargar();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private async Task Anterior() { if (pagina > 1) { pagina--; await Cargar(); } }
    private async Task Siguiente() { if (pagina < resultado.Paginas) { pagina++; await Cargar(); } }

    private static string ClaseEstado(string estado) => estado switch
    {
        "Pendiente" => "bg-secondary",
        "Surtida" => "bg-primary",
        "Timbrada" => "bg-success",
        "Entregada" => "bg-info text-dark",
        "Cancelada" => "bg-dark",
        "Devolucion" => "bg-danger",
        _ => "bg-secondary"
    };
}

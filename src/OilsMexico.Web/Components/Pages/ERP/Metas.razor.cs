using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Web.Components.Pages.ERP;

/// <summary>
/// Pantalla A del documento: Panel de Control de Metas (Admin).
/// <para>
/// Incluye también el tabulador por presentación y el cierre mensual, porque las tres cosas son
/// configuración administrativa del mismo módulo: darlas en pantallas separadas obligaría a
/// navegar de un lado a otro para capturar un solo vendedor.
/// </para>
/// Un Vendedor entra sólo a consultar SU avance; el panel de captura y el cierre están
/// protegidos por rol (ver Metas.razor y los <c>if (!EsAdmin)</c> de abajo).
/// </summary>
public partial class Metas : ComponentBase
{
    [Inject] private IComisionesService Comisiones { get; set; } = default!;
    [Inject] private ErpDbContext Db { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string msg = string.Empty;
    private bool err, guardando, cerrando;

    private List<VendedorItem> vendedores = [];
    private VendedorItem? vendSel;

    private int anio = DateTime.Now.Year;
    private int mes = DateTime.Now.Month;

    private List<CuotaDto> cuotas = [];

    // Captura de la cuota del vendedor/periodo elegido.
    private int? cuotaId;
    private string chkDinero = "", txtMetaDinero = "";
    private string chkLitros = "", txtMetaLitros = "";
    private OperadorLogico operador = OperadorLogico.SoloDinero;
    private AccionIncumplimiento accion = AccionIncumplimiento.CeroComision;
    private string txtPagoMinimo = "";
    private string txtNotas = "";

    private ComisionCalculoDto? calc;
    private List<ComisionHistorialDto> historial = [];
    private List<TabuladorDto> tabulador = [];
    private string filtroProd = "";

    private bool EsAdmin => Sesion.Sesion?.Rol == "Admin";
    private int VendedorActual => EsAdmin ? (vendSel?.Id ?? 0) : (Sesion.Sesion?.UsuarioId ?? 0);
    private string Periodo => $"{anio}-{mes:D2}";

    protected override async Task OnInitializedAsync()
    {
        if (!Sesion.Autenticado) return;
        await CargarVendedores();
        await CargarTabulador();
        if (VendedorActual > 0) await CargarTodo();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (Sesion.Sesion?.Rol is not ("Admin" or "Vendedor")) { Nav.NavigateTo("/"); return; }
        // En el prerrender la sesión aún no existe: si no se cargó nada, cargar ahora.
        if (vendedores.Count == 0)
        {
            await CargarVendedores();
            await CargarTabulador();
            if (VendedorActual > 0) await CargarTodo();
            StateHasChanged();
        }
    }

    private async Task CargarVendedores()
    {
        vendedores = await Db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && (u.Rol == "Vendedor" || u.Rol == "Admin"))
            .OrderBy(u => u.Nombre)
            .Select(u => new VendedorItem(u.Id, u.Nombre, u.Rol))
            .ToListAsync();
        vendSel ??= vendedores.FirstOrDefault();
    }

    /// <summary>Recarga cuota, cálculo en vivo e historial del vendedor+periodo elegidos.</summary>
    private async Task CargarTodo()
    {
        var vid = VendedorActual;
        if (vid == 0) return;

        cuotas = await Comisiones.ListarCuotasAsync(vid, anio, mes);
        var cuota = cuotas.FirstOrDefault(c => c.VendedorId == vid && c.Anio == anio && c.Mes == mes);
        LimpiarForm(cuota);

        calc = await Comisiones.CalcularAsync(vid, anio, mes);
        historial = await Comisiones.ListarHistorialAsync(vid, anio);
    }

    private async Task Refrescar()
    {
        msg = ""; err = false;
        if (VendedorActual == 0) { msg = "Selecciona un vendedor."; err = true; return; }
        await CargarTodo();
    }

    private async Task Guardar()
    {
        msg = ""; err = false;
        if (!EsAdmin) { msg = "Sólo un administrador puede modificar metas."; err = true; return; }
        if (VendedorActual == 0) { msg = "Selecciona un vendedor."; err = true; return; }

        // Casilla activa SIN monto = error explícito (no «quitar la meta»): desmarcar la casilla
        // es la acción de quitar. Así no se guardan metas en 0, que se cumplirían solas.
        decimal? metaDinero = null;
        if (chkDinero == "true")
        {
            if (!decimal.TryParse(txtMetaDinero, out var md) || md <= 0)
            { msg = "Captura la meta de dinero (mayor a 0) o desmarca la casilla."; err = true; return; }
            metaDinero = md;
        }

        decimal? metaLitros = null;
        if (chkLitros == "true")
        {
            if (!decimal.TryParse(txtMetaLitros, out var ml) || ml <= 0)
            { msg = "Captura la meta de litros (mayor a 0) o desmarca la casilla."; err = true; return; }
            metaLitros = ml;
        }

        // Coherencia del operador: no se puede exigir una meta que no quedó activa. El servicio
        // también valida, pero aquí se corrige sin perder lo capturado.
        var error = operador switch
        {
            OperadorLogico.SoloDinero => metaDinero is null ? "«Sólo dinero» requiere una meta de dinero." : null,
            OperadorLogico.SoloVolumen => metaLitros is null ? "«Sólo litros» requiere una meta de litros." : null,
            OperadorLogico.Y => (metaDinero is null || metaLitros is null) ? "«Y» requiere ambas metas activas." : null,
            _ => null // «O» opera con una o con las dos
        };
        if (error is not null) { msg = error; err = true; return; }

        if (!decimal.TryParse(txtPagoMinimo, out var pagoMinimo) || pagoMinimo < 0) pagoMinimo = 0m;

        guardando = true;
        try
        {
            await Comisiones.GuardarCuotaAsync(new GuardarCuotaRequest(
                cuotaId, VendedorActual, anio, mes, metaDinero, metaLitros,
                operador, accion, pagoMinimo, string.IsNullOrWhiteSpace(txtNotas) ? null : txtNotas.Trim()));
            await CargarTodo();
            msg = $"Cuota de {Periodo} guardada.";
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        guardando = false;
    }

    private async Task Cerrar()
    {
        msg = ""; err = false;
        if (!EsAdmin) { msg = "Sólo un administrador puede cerrar un periodo."; err = true; return; }
        if (VendedorActual == 0) return;
        cerrando = true;
        try
        {
            var h = await Comisiones.CerrarPeriodoAsync(new CerrarPeriodoRequest(VendedorActual, anio, mes, null));
            msg = $"Periodo {Periodo} cerrado: comisión congelada de {h.ComisionFinal:C}.";
            await CargarTodo();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
        cerrando = false;
    }

    private async Task Pagar(int id)
    {
        msg = ""; err = false;
        try
        {
            await Comisiones.MarcarPagadoAsync(id);
            msg = "Comisión marcada como pagada.";
            await CargarTodo();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private async Task Reabrir(int id)
    {
        msg = ""; err = false;
        try
        {
            await Comisiones.ReabrirPeriodoAsync(id, "Reabierto desde el panel de metas para corrección");
            msg = "Periodo reabierto: ya puedes recalcular el cierre.";
            await CargarTodo();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    // =====================================================================================
    // Tabulador por presentación
    // =====================================================================================

    private async Task CargarTabulador() => tabulador = await Comisiones.ListarTabuladorAsync(null);

    private async Task GuardarTabulador(TabuladorDto t)
    {
        msg = ""; err = false;
        if (!EsAdmin) { msg = "Sólo un administrador puede modificar el tabulador."; err = true; return; }
        if (t.PorcComisionBono < t.PorcComisionBase)
        { msg = $"El % de bono no puede ser menor al base en {t.Producto} ({t.Unidad})."; err = true; return; }
        if (t.PorcComisionBono > 100)
        { msg = $"El % de bono de {t.Producto} ({t.Unidad}) excede 100%."; err = true; return; }
        try
        {
            await Comisiones.GuardarTabuladorAsync(
                new GuardarTabuladorRequest(t.UnidadMedidaId, t.PorcComisionBase, t.PorcComisionBono));
            msg = $"Tabulador de {t.Producto} ({t.Unidad}) actualizado.";
            await CargarTabulador();
            if (calc is not null) await CargarTodo();
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private IEnumerable<TabuladorDto> TabuladorFiltrado =>
        string.IsNullOrWhiteSpace(filtroProd)
            ? tabulador
            : tabulador.Where(t => (t.Producto + " " + t.Sku + " " + t.Unidad)
                .Contains(filtroProd, StringComparison.OrdinalIgnoreCase));

    private async Task CambioVendedor(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var id))
            vendSel = vendedores.FirstOrDefault(v => v.Id == id);
        await CargarTodo();
    }

    private async Task CambioAnio(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var a)) anio = a;
        await CargarTodo();
    }

    private async Task CambioMes(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var m)) mes = m;
        await CargarTodo();
    }

    /// <summary>Vendedor que puede recibir comisiones (mismo criterio que Clientes y VentasService).</summary>
    private sealed record VendedorItem(int Id, string Nombre, string Rol);

    // =====================================================================================
    // Helpers de presentación
    // =====================================================================================

    /// <summary>Año actual y el anterior: es el rango realista para capturar/corregir metas.</summary>
    private static int[] Anios => [DateTime.Now.Year, DateTime.Now.Year - 1];

    private static readonly string[] Meses =
        ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
         "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    private static string MesNombre(int m) => m is >= 1 and <= 12 ? Meses[m - 1] : m.ToString();

    /// <summary>
    /// Ancho de la barra de progreso (0-100). Se recorta en 100 para que un cumplimiento de 250%
    /// no desborde la barra, pero el número al lado sigue mostrando el porcentaje real.
    /// </summary>
    private static double Ancho(decimal pct) => (double)Math.Min(pct, 100m);

    /// <summary>Clase Bootstrap según el estado del cierre (evita depender de CSS propio).</summary>
    private static string EstadoBadge(EstadoComision e) => e switch
    {
        EstadoComision.Borrador => "badge text-bg-secondary",
        EstadoComision.Cerrado => "badge text-bg-primary",
        EstadoComision.Pagado => "badge text-bg-success",
        _ => "badge text-bg-secondary"
    };

    private async Task EliminarCuota()
    {
        msg = ""; err = false;
        if (!EsAdmin) { msg = "Sólo un administrador puede eliminar metas."; err = true; return; }
        if (cuotaId is null) return;
        try
        {
            await Comisiones.EliminarCuotaAsync(cuotaId.Value);
            await CargarTodo();
            msg = "Cuota eliminada.";
        }
        catch (Exception ex) { msg = "Error: " + ex.Message; err = true; }
    }

    private void LimpiarForm(CuotaDto? c)
    {
        cuotaId = c?.Id;
        txtMetaDinero = c?.MetaDinero?.ToString("0.##") ?? "";
        txtMetaLitros = c?.MetaLitros?.ToString("0.##") ?? "";
        chkDinero = c?.MetaDinero is not null ? "true" : "";
        chkLitros = c?.MetaLitros is not null ? "true" : "";
        operador = c?.Operador ?? OperadorLogico.SoloDinero;
        accion = c?.AccionIncumplimiento ?? AccionIncumplimiento.CeroComision;
        txtPagoMinimo = (c?.MontoPagoMinimo ?? 0m).ToString("0.##");
        txtNotas = c?.Notas ?? "";
    }

    /// <summary>«Descartar cambios»: recarga la cuota guardada y pisa lo capturado a medias.</summary>
    private async Task RestaurarForm()
    {
        msg = ""; err = false;
        cuotas = await Comisiones.ListarCuotasAsync(VendedorActual, anio, mes);
        LimpiarForm(cuotas.FirstOrDefault(c => c.VendedorId == VendedorActual && c.Anio == anio && c.Mes == mes));
    }

    // Las casillas se llevan como string y no como bool a propósito: <input type="checkbox">
    // con @bind sobre un bool? obliga a distinguir null de false, y aquí el tercer estado
    // («no hay meta capturada») no existe. Con string, "" es apagado y "true" es encendido.
    private void CambioChkDinero(ChangeEventArgs e) => chkDinero = (bool)e.Value! ? "true" : "";
    private void CambioChkLitros(ChangeEventArgs e) => chkLitros = (bool)e.Value! ? "true" : "";
}


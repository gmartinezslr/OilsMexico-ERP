using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;

namespace OilsMexico.Web.Components.Pages.ERP;

public partial class Diferenciadores
{
    [Inject] private IDiferenciadoresService Servicio { get; set; } = default!;
    [Inject] private IAutorizacionService Autorizacion { get; set; } = default!;
    [Inject] private IVentasPosService Ventas { get; set; } = default!;
    [Inject] private IClientesService ClientesSvc { get; set; } = default!;
    [Inject] private IComprasService ComprasSvc { get; set; } = default!;
    [Inject] private ISucursalContext SucCtx { get; set; } = default!;
    [Inject] private ISesionActual Sesion { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string pestaña = "precios";
    private string mensaje = "";
    private bool error, cargando, guardando;
    private List<ProductoDto> productos = [];
    private List<(int Id, string Nombre)> clientes = [];
    private List<CompraListadoDto> comprasRecibidas = [];
    private List<ListaPrecioDto> listasPrecio = [];
    private List<PrecioVolumenDto> preciosVolumen = [];
    private List<CompatibilidadVehiculoDto> compatibilidades = [];
    private List<EvaluacionProveedorDto> evaluaciones = [];
    private List<(int Id, string Folio, string Cliente)> ordenes = [];
    private List<EntregaRepartoDto> entregas = [];
    private List<ControlTamborDto> controlesTambor = [];
    private List<EnvaseSaldoDto> saldosEnvase = [];
    private List<EntregaLineaEdicion> lineasEntrega = [];
    private EntregaRepartoDto? entregaSeleccionada;
    private CartaPorteDto? cartaPorte;

    private int listaId, precioId, precioProductoId;
    private string listaNombre = "", listaTipo = "menudeo";
    private bool listaActiva = true;
    private decimal precioCantidadMinima = 1, precioUnitario;

    private int compatibilidadId, compatProductoId;
    private string compatMarca = "", compatModelo = "", compatMotor = "", compatViscosidad = "", compatNorma = "";
    private int compatAnioDesde = DateTime.UtcNow.Year - 5, compatAnioHasta = DateTime.UtcNow.Year;
    private decimal? compatCapacidad;

    private int compraEvaluarId, cumplimiento = 5, puntualidad = 5;
    private string comentariosEvaluacion = "";

    private int ordenEntregaId;
    private string ruta = "", chofer = "", vehiculo = "", placas = "";
    private DateTime fechaProgramada = DateTime.Now.AddHours(1);

    private int tamborId, tamborProductoId, tamboresCerrados;
    private decimal litrosPorTambor = 208, litrosAbiertos;

    private int envaseClienteId, envaseCantidad = 1;
    private string envaseNombre = "", envaseReferencia = "";
    private decimal envaseDeposito;
    private bool esDevolucion;

    private int cpEntregaId;
    private string cpRfc = "", cpPermiso = "", cpNumeroPermiso = "", cpConfiguracion = "", cpPlacas = "";
    private decimal cpPeso;
    private string cpOrigen = "", cpDestino = "";

    private sealed class EntregaLineaEdicion
    {
        public int ProductoId { get; init; }
        public string Producto { get; init; } = "";
        public decimal Programada { get; init; }
        public decimal Entregada { get; set; }
        public decimal Devuelta { get; set; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        if (!Sesion.Autenticado && !await Sesion.RestaurarAsync()) { Nav.NavigateTo("/login"); return; }
        if (!await Autorizacion.TienePermisoAsync(PermisosPaginas.Nombre("diferenciadores"))) { Nav.NavigateTo("/"); return; }
        await Cargar();
        StateHasChanged();
    }

    private async Task Cargar()
    {
        cargando = true;
        try
        {
            var sucursal = SucCtx.SucursalId;
            listasPrecio = await Servicio.ListarListasPrecioAsync();
            compatibilidades = await Servicio.ListarCompatibilidadesAsync();
            evaluaciones = await Servicio.ListarEvaluacionesProveedorAsync();
            entregas = await Servicio.ListarEntregasAsync();
            ordenes = await Servicio.OrdenesParaEntregaAsync();
            controlesTambor = await Servicio.ListarControlesTamborAsync();
            saldosEnvase = await Servicio.ListarSaldosEnvasesAsync();
            productos = await Ventas.BuscarProductosAsync(sucursal, "");
            clientes = (await ClientesSvc.ListarParaVentaAsync()).Select(x => (x.Id, x.Nombre)).ToList();
            var evaluadas = evaluaciones.Select(x => x.CompraId).ToHashSet();
            comprasRecibidas = (await ComprasSvc.ListarAsync(sucursal, EstadoCompra.Recibida, null))
                .Where(x => !evaluadas.Contains(x.CompraId)).ToList();
            if (listaId == 0 && listasPrecio.Count > 0) listaId = listasPrecio[0].Id;
            if (listaId > 0) await SeleccionarLista();
            if (ordenEntregaId == 0 && ordenes.Count > 0) ordenEntregaId = ordenes[0].Id;
            mensaje = "";
        }
        catch (Exception ex) { mensaje = $"No se pudieron cargar los diferenciadores: {ex.Message}"; error = true; }
        finally { cargando = false; }
    }

    private async Task CargarPreciosVolumen()
    {
        preciosVolumen = await Servicio.ListarPreciosVolumenAsync(listaId);
    }

    private async Task GuardarListaPrecio()
    {
        await Ejecutar(async () =>
        {
            listaId = await Servicio.GuardarListaPrecioAsync(listaId, listaNombre, listaTipo, listaActiva);
            await Cargar();
        }, "Lista de precios guardada.");
    }

    private async Task GuardarPrecioVolumen()
    {
        await Ejecutar(async () =>
        {
            await Servicio.GuardarPrecioVolumenAsync(precioId, listaId, precioProductoId, precioCantidadMinima, precioUnitario);
            precioId = 0;
            await CargarPreciosVolumen();
        }, "Regla de precio escalonado guardada.");
    }

    private async Task GuardarCompatibilidad()
    {
        await Ejecutar(async () =>
        {
            await Servicio.GuardarCompatibilidadAsync(compatibilidadId, new CompatibilidadVehiculoRequest(
                compatMarca, compatModelo, compatAnioDesde, compatAnioHasta, compatMotor, compatViscosidad,
                compatNorma, compatCapacidad, compatProductoId == 0 ? null : compatProductoId));
            compatibilidadId = 0;
            compatMarca = compatModelo = compatMotor = compatViscosidad = compatNorma = "";
            compatCapacidad = null;
            compatProductoId = 0;
            await Cargar();
        }, "Compatibilidad guardada.");
    }

    private async Task GuardarEvaluacion()
    {
        await Ejecutar(async () =>
        {
            await Servicio.EvaluarProveedorAsync(new EvaluacionProveedorRequest(
                compraEvaluarId, cumplimiento, puntualidad, comentariosEvaluacion));
            comentariosEvaluacion = "";
            await Cargar();
        }, "Evaluación de recepción guardada.");
    }

    private async Task ProgramarEntrega()
    {
        await Ejecutar(async () =>
        {
            await Servicio.ProgramarEntregaAsync(new EntregaCrearRequest(
                ordenEntregaId, ruta, chofer, vehiculo, placas, fechaProgramada));
            ruta = chofer = vehiculo = placas = "";
            await Cargar();
        }, "Reparto programado. La salida de inventario ya ocurrió al facturar y no se repite aquí.");
    }

    private void PrepararLiquidacion(EntregaRepartoDto entrega)
    {
        entregaSeleccionada = entrega;
        lineasEntrega = entrega.Detalles.Select(x => new EntregaLineaEdicion
        {
            ProductoId = x.ProductoId, Producto = x.Producto, Programada = x.Programada,
            Entregada = x.Entregada, Devuelta = x.Devuelta
        }).ToList();
    }

    private async Task LiquidarEntrega()
    {
        if (entregaSeleccionada is null) return;
        await Ejecutar(async () =>
        {
            await Servicio.LiquidarEntregaAsync(new EntregaLiquidarRequest(
                entregaSeleccionada.Id,
                lineasEntrega.Select(x => new EntregaLiquidarRenglonDto(x.ProductoId, x.Entregada, x.Devuelta)).ToList()));
            entregaSeleccionada = null;
            await Cargar();
        }, "Liquidación de entrega registrada.");
    }

    private async Task GuardarControlTambor()
    {
        await Ejecutar(async () =>
        {
            await Servicio.GuardarControlTamborAsync(tamborId, tamborProductoId, litrosPorTambor, tamboresCerrados, litrosAbiertos);
            tamborId = 0;
            await Cargar();
        }, "Control físico de tambores guardado.");
    }

    private async Task RegistrarEnvase()
    {
        await Ejecutar(async () =>
        {
            await Servicio.RegistrarMovimientoEnvaseAsync(new EnvaseMovimientoRequest(
                envaseClienteId, envaseNombre, envaseCantidad, envaseDeposito, esDevolucion, envaseReferencia));
            await Cargar();
        }, esDevolucion ? "Devolución de envases registrada." : "Entrega de envases y depósito registrado.");
    }

    private async Task GuardarCartaPorte()
    {
        await Ejecutar(async () =>
        {
            cartaPorte = await Servicio.GuardarCartaPorteAsync(new CartaPorteRequest(
                cpEntregaId, cpRfc, cpPermiso, cpNumeroPermiso, cpConfiguracion, cpPlacas,
                cpPeso, cpOrigen, cpDestino));
            await Cargar();
        }, "Borrador Carta Porte 3.1 guardado. No es un CFDI y no está timbrado.");
    }

    private async Task Ejecutar(Func<Task> accion, string exito)
    {
        guardando = true;
        mensaje = "";
        try { await accion(); mensaje = exito; error = false; }
        catch (Exception ex) { mensaje = $"No se pudo guardar: {ex.Message}"; error = true; }
        finally { guardando = false; }
    }

    private async Task SeleccionarLista()
    {
        precioId = 0;
        if (listaId == 0)
        {
            listaNombre = "";
            listaTipo = "menudeo";
            listaActiva = true;
            preciosVolumen = [];
            return;
        }
        var lista = listasPrecio.FirstOrDefault(x => x.Id == listaId);
        if (lista is not null)
        {
            listaNombre = lista.Nombre;
            listaTipo = lista.TipoCliente;
            listaActiva = lista.Activa;
        }
        await CargarPreciosVolumen();
    }

    private void EditarPrecio(PrecioVolumenDto precio)
    {
        precioId = precio.Id;
        precioProductoId = precio.ProductoId;
        precioCantidadMinima = precio.CantidadMinima;
        precioUnitario = precio.PrecioUnitario;
    }

    private void EditarCompatibilidad(CompatibilidadVehiculoDto x)
    {
        compatibilidadId = x.Id; compatMarca = x.Marca; compatModelo = x.Modelo; compatAnioDesde = x.AnioDesde;
        compatAnioHasta = x.AnioHasta; compatMotor = x.Motor; compatViscosidad = x.Viscosidad;
        compatNorma = x.Norma ?? ""; compatCapacidad = x.CapacidadLitros; compatProductoId = x.ProductoId ?? 0;
    }

    private void EditarTambor(ControlTamborDto x)
    {
        tamborId = x.Id; tamborProductoId = x.ProductoId; litrosPorTambor = x.LitrosPorTambor;
        tamboresCerrados = x.TamboresCerrados; litrosAbiertos = x.LitrosAbiertos;
    }

    private void PrepararCartaPorte(EntregaRepartoDto entrega)
    {
        cpEntregaId = entrega.Id; cpPlacas = entrega.Placas ?? "";
        cpRfc = cpPermiso = cpNumeroPermiso = cpConfiguracion = cpOrigen = cpDestino = "";
        cpPeso = 0; cartaPorte = null;
    }
}

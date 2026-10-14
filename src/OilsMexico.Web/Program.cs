using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Application.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using OilsMexico.Web.Components;
using OilsMexico.Web.Hubs;
using OilsMexico.Web.Services;
using QuestPDF;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF 2025.x requiere declarar la licencia una sola vez al arrancar.
// Community: gratuita para empresas con <1M USD de facturación anual (ver licencia del paquete).
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<ErpDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("ErpDb")),
    contextLifetime: ServiceLifetime.Transient,
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddScoped<ISucursalContext, SucursalContext>();
// Fábrica de ámbitos para lecturas puntuales desde componentes Blazor sin acoplarlos a ErpDbContext (DIP).
builder.Services.AddSingleton<IVentaEscopoFactory, VentaEscopoFactory>();
builder.Services.AddScoped<ISesionActual, SesionActual>();
builder.Services.AddScoped<IAvisos, AvisosService>();
builder.Services.AddScoped<ITimeoutSesion, TimeoutSesion>();
builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICatalogosSatService, CatalogosSatService>();
builder.Services.AddScoped<IPoliticaPassword, PoliticaPasswordService>();
builder.Services.AddSingleton<IReloj, RelojSistema>();
builder.Services.AddScoped<IVentasService, VentasService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IAlmacenConsulta, AlmacenConsultaService>();
builder.Services.AddScoped<ICatalogosService, CatalogosService>();
// Lecturas de panel y catálogos de combos: evitan que las páginas Blazor inyecten ErpDbContext (DIP).
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICatalogoConsultaService, CatalogoConsultaService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICfdiSelladoService, CfdiSelladoService>();
// Cliente HTTP del PAC Finkok (named client con timeout); PacTimbradoService lo recibe vía DI.
builder.Services.AddHttpClient("finkok", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IPacTimbradoService>(sp => new PacTimbradoService(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("finkok"),
    sp.GetRequiredService<ErpDbContext>()));
builder.Services.AddScoped<IFacturacionService, FacturacionService>();
builder.Services.AddScoped<INotaCreditoService, NotaCreditoService>();
builder.Services.AddScoped<IComplementoPagoService, ComplementoPagoService>();
builder.Services.AddScoped<ICorteCajaService, CorteCajaService>();
    builder.Services.AddScoped<IReportesFinancierosService, ReportesFinancierosService>();
    builder.Services.AddScoped<IExportacionService, ExportacionService>();

builder.Services.AddScoped<IComprasService, ComprasService>();
    builder.Services.AddScoped<ICuentasContablesService, CuentasContablesService>();
    builder.Services.AddScoped<ICierreContableService, CierreContableService>();
    builder.Services.AddScoped<IEstadoCuentasService, EstadoCuentasService>();
    builder.Services.AddScoped<IGestionService, GestionService>();
    builder.Services.AddScoped<ICobranzaService, CobranzaService>();
    builder.Services.AddScoped<IComisionesService, ComisionesService>();
    builder.Services.AddScoped<IBusquedaGlobalService, BusquedaGlobalService>();
    builder.Services.AddScoped<IAsientoGeneradorService, AsientoGeneradorService>();
    builder.Services.AddScoped<IBancosService, BancosService>();
    builder.Services.AddScoped<ICotizacionesService, CotizacionesService>();
    builder.Services.AddScoped<IGastoService, GastoService>();
    // Correo saliente (SMTP): remitente y servidor configurables en Configuración (tabla configuracion).
    builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<ISepomexService, SepomexService>();
builder.Services.AddScoped<IImportacionProductosService, ImportacionProductosService>();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(o => o.DetailedErrors = true);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    await SeedData.InicializarAsync(db);

    if (args.Contains("--importar-aceites"))
    {
        var importador = scope.ServiceProvider.GetRequiredService<IImportacionProductosService>();
        // Ruta del catálogo (A8): fuente única = configuración. Se lee de
        // Importacion:RutaCatalogo (appsettings.json, user-secrets o la variable de entorno
        // Importacion__RutaCatalogo). Si es relativa, se resuelve contra el directorio actual;
        // si no se configura nada, se busca "lista_precios_aceites.xlsx" junto al ejecutable.
        // Ya no se asume ninguna ruta absoluta ni de desarrollo embebida en el código.
        var ruta = builder.Configuration["Importacion:RutaCatalogo"] ?? "";
        if (!string.IsNullOrWhiteSpace(ruta) && !Path.IsPathRooted(ruta))
            ruta = Path.GetFullPath(ruta);
        if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
            ruta = Path.Combine(Directory.GetCurrentDirectory(), "lista_precios_aceites.xlsx");
        if (!File.Exists(ruta))
        {
            Console.WriteLine("[CLI] No se encontró el catálogo de aceites. Configure Importacion:RutaCatalogo " +
                              "(appsettings.json, user-secrets o la variable de entorno Importacion__RutaCatalogo) " +
                              "con la ruta del archivo lista_precios_aceites.xlsx.");
            return;
        }
        Console.WriteLine($"[CLI] Importando aceites desde: {ruta}");
        var res = await importador.ImportarDesdeArchivoLocalAsync(ruta, actualizarExistentes: true);
        Console.WriteLine($"[CLI] {res.Mensaje}");
        var totalProds = await db.Productos.CountAsync();
        Console.WriteLine($"[CLI] Total de productos en la base de datos: {totalProds}");
        return;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.UseAntiforgery();
app.MapHub<ErpHub>("/hubs/erp");

// Salud: lo usa la pantalla de arranque (iniciando-erp.html) para saber
// cuándo el servidor ya acepta conexiones y redirigir al ERP sin mostrar
// el error ERR_CONNECTION_REFUSED del navegador.
app.MapGet("/health", () => Results.Ok("ok"));

// Descarga de XML CFDI (archivo). El acceso exige el token de sesión vigente (?t=...), el mismo
// control que las páginas Blazor: usuario activo, sin bloqueo vigente y sesión no revocada.
app.MapGet("/api/facturas/{id:int}/xml", async (int id, ErpDbContext db, HttpContext ctx) =>
{
    if (!await TokenSesionValidoAsync(db, ctx)) return Results.Unauthorized();
    var f = await db.Facturas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (f is null || string.IsNullOrEmpty(f.XmlSellado)) return Results.NotFound();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(f.XmlSellado),
        "application/xml", $"{f.FolioInterno}.xml");
});
app.MapGet("/api/notas-credito/{id:int}/xml", async (int id, ErpDbContext db, HttpContext ctx) =>
{
    if (!await TokenSesionValidoAsync(db, ctx)) return Results.Unauthorized();
    var n = await db.NotasCredito.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (n is null || string.IsNullOrEmpty(n.XmlSellado)) return Results.NotFound();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(n.XmlSellado),
        "application/xml", $"{n.FolioInterno}.xml");
});
app.MapGet("/api/reps/{id:int}/xml", async (int id, ErpDbContext db, HttpContext ctx) =>
{
    if (!await TokenSesionValidoAsync(db, ctx)) return Results.Unauthorized();
    var r = await db.ComplementosPago.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (r is null || string.IsNullOrEmpty(r.XmlSellado)) return Results.NotFound();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(r.XmlSellado),
        "application/xml", $"{r.FolioInterno}.xml");
});

// ============================================================================
// Exportación Excel/PDF (checklist §2.1). Descarga directa de archivos generados
// en memoria por IExportacionService. Exigen token de sesión vigente (?t=...),
// igual que la descarga de XML. REGLA #1 la aplica el servicio vía ISucursalContext.
// ============================================================================

// Helper local: valida token, ejecuta la exportación y devuelve el archivo.
async Task<IResult> DescargarAsync(HttpContext ctx, Func<Task<ArchivoExportadoDto>> generar, ErpDbContext? db = null)
{
    if (!await TokenSesionValidoAsync(ctx.RequestServices.GetRequiredService<ErpDbContext>(), ctx))
        return Results.Unauthorized();
    var archivo = await generar();
    return Results.File(archivo.Contenido, archivo.MimeTipo, archivo.NombreArchivo);
}

app.MapGet("/api/export/estado-cuenta/{clienteId:int}", async (int clienteId, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.EstadoCuentaClienteAsync(clienteId)));

app.MapGet("/api/export/cxc/{sucursalId:int}", async (int sucursalId, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.CxPendientesAsync(sucursalId)));

app.MapGet("/api/export/cxp/{sucursalId:int}", async (int sucursalId, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.CxPPendientesAsync(sucursalId)));

app.MapGet("/api/export/inventario/{sucursalId:int}", async (int sucursalId, string? filtro, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.InventarioAsync(sucursalId, filtro)));

app.MapGet("/api/export/kardex/{sucursalId:int}/{productoId:int}", async (int sucursalId, int productoId, int? dias, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.KardexAsync(sucursalId, productoId, dias ?? 30)));

app.MapGet("/api/export/ventas/{sucursalId:int}", async (int sucursalId, DateTime desde, DateTime hasta, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.VentasAsync(sucursalId, desde, hasta)));

app.MapGet("/api/export/comisiones", async (int? sucursalId, DateTime desde, DateTime hasta, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.ComisionesAsync(sucursalId, desde, hasta)));

app.MapGet("/api/export/gastos/{sucursalId:int}", async (int sucursalId, DateTime desde, DateTime hasta, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.GastosAsync(sucursalId, desde, hasta)));

app.MapGet("/api/export/lista-precios", async (IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.ListaPreciosAsync()));

// PDFs de documentos
app.MapGet("/api/export/factura/{facturaId:int}/pdf", async (int facturaId, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.FacturaCfdiPdfAsync(facturaId)));

app.MapGet("/api/export/cotizacion/{cotizacionId:int}/pdf", async (int cotizacionId, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.CotizacionPdfAsync(cotizacionId)));

app.MapGet("/api/export/orden/{ordenId:int}/pdf", async (int ordenId, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.OrdenVentaPdfAsync(ordenId)));

// PDF de un estado financiero (§1.4): reporte = balanza | balance | resultados | existencias
app.MapGet("/api/export/financiero/{reporte}/{sucursalId:int}", async (string reporte, int sucursalId, DateTime desde, DateTime hasta, IExportacionService exp, HttpContext ctx) =>
    await DescargarAsync(ctx, () => exp.FinancieroPdfAsync(reporte, sucursalId, desde, hasta)));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Valida el token de sesión de las descargas XML (?t=...). El token se emite al iniciar sesión y
// se revoca al desbloquear, desactivar o cambiar la contraseña, de modo que un usuario bloqueado
// o desactivado pierde el acceso sin esperar a que cierre el navegador.
static async Task<bool> TokenSesionValidoAsync(ErpDbContext db, HttpContext ctx)
{
    var t = ctx.Request.Query["t"].ToString();
    if (string.IsNullOrWhiteSpace(t)) return false;
    var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.SesionToken == t);
    return u is { Activo: true, BloqueadoDefinitivamente: false }
        && (u.BloqueadoHastaUtc is null || u.BloqueadoHastaUtc <= DateTime.UtcNow);
}

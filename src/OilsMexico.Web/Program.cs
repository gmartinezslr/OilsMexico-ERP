using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.Interfaces;
using OilsMexico.Application.Services;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;
using OilsMexico.Web.Components;
using OilsMexico.Web.Hubs;
using OilsMexico.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<ErpDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("ErpDb")));

builder.Services.AddScoped<ISucursalContext, SucursalContext>();
builder.Services.AddScoped<ISesionActual, SesionActual>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICatalogosSatService, CatalogosSatService>();
builder.Services.AddScoped<IVentasService, VentasService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IAlmacenConsulta, AlmacenConsultaService>();
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
builder.Services.AddScoped<IComprasService, ComprasService>();
    builder.Services.AddScoped<ICuentasContablesService, CuentasContablesService>();
    builder.Services.AddScoped<IEstadoCuentasService, EstadoCuentasService>();
    builder.Services.AddScoped<IGestionService, GestionService>();
    builder.Services.AddScoped<IAsientoGeneradorService, AsientoGeneradorService>();

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
        var ruta = Path.Combine(Directory.GetCurrentDirectory(), "lista_precios_aceites.xlsx");
        if (!File.Exists(ruta)) ruta = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "lista_precios_aceites.xlsx"));
        if (!File.Exists(ruta)) ruta = @"D:\GMS\Jerry\OilsMexico\lista_precios_aceites.xlsx";
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

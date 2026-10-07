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
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(o => o.DetailedErrors = true);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    await SeedData.InicializarAsync(db);
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

// Descarga de XML CFDI (archivo). Mismo modelo de acceso que el resto del app (uso interno;
// la sesión se valida en el circuito de Blazor, no en endpoints mínimos).
app.MapGet("/api/facturas/{id:int}/xml", async (int id, ErpDbContext db) =>
{
    var f = await db.Facturas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (f is null || string.IsNullOrEmpty(f.XmlSellado)) return Results.NotFound();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(f.XmlSellado),
        "application/xml", $"{f.FolioInterno}.xml");
});
app.MapGet("/api/notas-credito/{id:int}/xml", async (int id, ErpDbContext db) =>
{
    var n = await db.NotasCredito.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (n is null || string.IsNullOrEmpty(n.XmlSellado)) return Results.NotFound();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(n.XmlSellado),
        "application/xml", $"{n.FolioInterno}.xml");
});
app.MapGet("/api/reps/{id:int}/xml", async (int id, ErpDbContext db) =>
{
    var r = await db.ComplementosPago.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (r is null || string.IsNullOrEmpty(r.XmlSellado)) return Results.NotFound();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(r.XmlSellado),
        "application/xml", $"{r.FolioInterno}.xml");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

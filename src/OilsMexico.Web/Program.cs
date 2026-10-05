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
builder.Services.AddScoped<IPacTimbradoService, PacTimbradoService>();
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

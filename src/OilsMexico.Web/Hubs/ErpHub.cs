using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Web.Hubs;

/// <summary>
/// Tiempo real entre sucursales: cada caja se suscribe a su grupo sucursal-{id}.
/// La conexión exige un token de sesión vigente (?t=...) emitido al iniciar sesión.
/// </summary>
public sealed class ErpHub(ErpDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["t"].ToString();
        var valido = !string.IsNullOrWhiteSpace(token) &&
            await db.Usuarios.AsNoTracking().AnyAsync(u =>
                u.SesionToken == token && u.Activo && !u.BloqueadoDefinitivamente &&
                (u.BloqueadoHastaUtc == null || u.BloqueadoHastaUtc <= DateTime.UtcNow));
        if (!valido)
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }

    public Task UnirseASucursal(int sucursalId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"sucursal-{sucursalId}");
}

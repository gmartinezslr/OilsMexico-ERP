using Microsoft.AspNetCore.SignalR;

namespace OilsMexico.Web.Hubs;

/// <summary>Tiempo real entre sucursales: cada caja se suscribe a su grupo sucursal-{id}.</summary>
public sealed class ErpHub : Hub
{
    public Task UnirseASucursal(int sucursalId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"sucursal-{sucursalId}");
}

using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Web.Services;

/// <summary>Sesión persistente por navegador: sobrevive navegaciones y recargas del circuito.</summary>
public sealed class SesionActual(ProtectedSessionStorage storage, ISucursalContext ctx) : ISesionActual
{
    private const string Clave = "oilsmexico.sesion";
    public SesionDto? Sesion { get; private set; }
    public bool Autenticado => Sesion is not null;

    public async Task IniciarAsync(SesionDto sesion)
    {
        Sesion = sesion;
        ctx.Establecer(sesion.SucursalId, sesion.UsuarioId, sesion.Rol);
        await storage.SetAsync(Clave, sesion);
    }

    public async Task CerrarAsync()
    {
        Sesion = null;
        await storage.DeleteAsync(Clave);
    }

    public async Task<bool> RestaurarAsync()
    {
        try
        {
            var r = await storage.GetAsync<SesionDto>(Clave);
            if (r.Success && r.Value is not null)
            {
                Sesion = r.Value;
                ctx.Establecer(Sesion.SucursalId, Sesion.UsuarioId, Sesion.Rol);
                return true;
            }
        }
        catch { }
        return false;
    }
}

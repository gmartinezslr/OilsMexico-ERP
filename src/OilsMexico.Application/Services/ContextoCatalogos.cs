using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Application.Services;

public sealed class SucursalContext : ISucursalContext
{
    public int SucursalId { get; private set; } = 1;
    public int UsuarioId { get; private set; } = 1;
    public string Rol { get; private set; } = "Admin";

    public void Establecer(int sucursalId, int usuarioId, string rol)
    {
        SucursalId = sucursalId;
        UsuarioId = usuarioId;
        Rol = rol;
    }
}

public sealed class CatalogosSatService : ICatalogosSatService
{
    public CatalogosSatDto Obtener() => new(
        new Dictionary<string, string>
        {
            ["01"] = "Efectivo",
            ["02"] = "Cheque nominativo",
            ["03"] = "Transferencia electrónica",
            ["04"] = "Tarjeta de crédito",
            ["28"] = "Tarjeta de débito",
            ["99"] = "Por definir"
        },
        new Dictionary<string, string>
        {
            ["PUE"] = "Pago en una sola exhibición",
            ["PPD"] = "Pago en parcialidades o diferido"
        },
        new Dictionary<string, string>
        {
            ["G01"] = "Adquisición de mercancías",
            ["G03"] = "Gastos en general",
            ["S01"] = "Sin efectos fiscales",
            ["CP01"] = "Pagos"
        },
        new Dictionary<string, string>
        {
            ["601"] = "General de Ley Personas Morales",
            ["612"] = "Personas Físicas con Actividades Empresariales",
            ["616"] = "Sin obligaciones fiscales",
            ["626"] = "Régimen Simplificado de Confianza"
        });
}

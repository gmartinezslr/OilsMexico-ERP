using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Application.Services;

public sealed class SucursalContext : ISucursalContext
{
    /// <summary>
    /// El default de un circuito sin sesión. Es un SENTINEL, NO un rol concedido:
    /// cualquier ruta que consulte <c>Rol == "Admin"</c> sin haber pasado por
    /// <see cref="Establecer"/> (login o restore) se comporta como ningún rol, no como Admin.
    /// Esto hace fail-closed todas las comprobaciones de permisos del ERP (fuerza el rewrite
    /// de ~40 sitios a una regla de negocio: «sin sesión real no se asciende ningún privilegio».
    /// El seed y el prerender no dependen de este default.
    /// </summary>
    public string Rol { get; private set; } = "SinSesion";

    /// <summary>
    /// ID de la sucursal manejada. Mantiene el valor 1 para que el prerender y el seed no se
    /// rompan: no es un permiso, es el "sucursal por defecto" de lectura sin sesión.
    /// </summary>
    public int SucursalId { get; private set; } = 1;

    /// <summary>
    /// Identidad que se graba en los audit trails. Sin sesión real no se puede confiar
    /// tampoco en atribuir la acción a un usuario: se mantiene 1 (valor de la BD, no de
    /// seguridad) para no romper FKs ni filas legacy, y toda escritura real obliga a
    /// <see cref="Establecer"/> antes de operar.
    /// </summary>
    public int UsuarioId { get; private set; } = 1;

    public bool Establecida { get; private set; }

    public void Establecer(int sucursalId, int usuarioId, string rol)
    {
        SucursalId = sucursalId;
        UsuarioId = usuarioId;
        Rol = rol;
        Establecida = true;
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

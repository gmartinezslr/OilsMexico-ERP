using OilsMexico.Application.Interfaces;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Autorización del módulo de comisiones (fase 3).
/// <para>
/// <b>Por qué aquí y no en la UI:</b> ocultar un botón en el .razor no protege nada. Blazor Server
/// no tiene un «backend» separado: el método del servicio es alcanzable desde cualquier componente
/// (o desde la consola del circuito) mientras haya sesión válida. Si el permiso viviera sólo en el
/// markup, un vendedor bastaría con invocar el método directamente. Estas reglas son la única
/// barrera real.
/// </para>
/// <para>
/// <b>Por qué se exige <c>Establecida</c>:</b> <c>ISucursalContext</c> trae defaults de Admin para
/// que el seed y el prerrender funcionen sin sesión. Autorizar por <c>Rol</c> sin revisar ese flag
/// sería FAIL-OPEN — un circuito sin sesión heredaría privilegios de administrador.
/// </para>
/// </summary>
public sealed partial class ComisionesService
{
    /// <summary>
    /// Rol de la sesión VIGENTE. Devuelve null cuando no hay sesión real, así que cualquier
    /// «no permitido» y «sin sesión» se tratan igual: no se filtra por qué falló.
    /// </summary>
    protected string? RolSesion => ctx.Establecida ? ctx.Rol : null;

    protected bool EsAdmin => RolSesion == "Admin";

    /// <summary>
    /// Identidad del usuario en sesión, ya validada. Null = no hay sesión real.
    /// </summary>
    protected int? UsuarioSesion => ctx.Establecida && ctx.UsuarioId > 0 ? ctx.UsuarioId : null;

    /// <summary>Exige sesión real y rol Admin. Para todo lo que cambia dinero o metas.</summary>
    protected void ExigirAdmin(string accion)
    {
        if (UsuarioSesion is null)
            throw new InvalidOperationException(
                $"No hay sesión activa: «{accion}» requiere iniciar sesión.");
        if (!EsAdmin)
            throw new UnauthorizedAccessException(
                $"Sólo un administrador puede {accion}. Tu rol es «{ctx.Rol}».");
    }

    /// <summary>
    /// Permite a un vendedor consultar SU propia información y a un Admin la de cualquiera.
    /// Es la regla del portal: sin ella, cualquier vendedor podría leer (y con eso, presionar o
    /// filtrar) la comisión de sus compañeros con sólo cambiar el id en la petición.
    /// </summary>
    protected void ExigirDuenoOAdmin(int vendedorId, string accion)
    {
        if (UsuarioSesion is null)
            throw new InvalidOperationException(
                $"No hay sesión activa: «{accion}» requiere iniciar sesión.");
        if (EsAdmin) return;
        if (EsRolComisionable(ctx.Rol) && ctx.UsuarioId == vendedorId) return;
        throw new UnauthorizedAccessException(
            $"No puedes consultar «{accion}» de otro vendedor.");
    }
}

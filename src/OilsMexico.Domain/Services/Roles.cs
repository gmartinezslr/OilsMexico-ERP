namespace OilsMexico.Domain.Services;

/// <summary>
/// Fuente única de los nombres de rol del ERP. Reemplaza el string mágico "Admin" (y los
/// demás roles) que estaba disperso por servicios y páginas: si se renombra un rol en la
/// base de datos, se cambia SOLO aquí y el resto del código sigue compilando y coherente.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Vendedor = "Vendedor";
    public const string Almacen = "Almacen";
    public const string Conta = "Conta";

    /// <summary>Roles asignables (orden de UI en Administración &gt; Usuarios).</summary>
    public static readonly string[] Todos = [Admin, Vendedor, Almacen, Conta];

    /// <summary>
    /// Etiqueta visible de cada rol para la UI (Administración &gt; Usuarios). Sustituye los
    /// <c>&lt;option&gt;</c> literales del combo de roles: agregar un rol nuevo aquí basta para
    /// que aparezca en el formulario.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Etiquetas =
        new Dictionary<string, string>
        {
            [Admin] = "Admin — Administración",
            [Vendedor] = "Vendedor — Ventas",
            [Almacen] = "Almacen — Almacén",
            [Conta] = "Conta — Contabilidad"
        };

    /// <summary>Etiqueta visible de un rol (fallback: el nombre tal cual).</summary>
    public static string Etiqueta(string rol) => Etiquetas.TryGetValue(rol, out var e) ? e : rol;
}

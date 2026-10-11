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
}

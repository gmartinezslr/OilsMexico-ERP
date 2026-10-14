namespace OilsMexico.Domain.Services;

public sealed record PermisoPagina(string Ruta, string Etiqueta, string[] RolesIniciales);

public static class PermisosPaginas
{
    public const string Acceder = "Acceder";

    private static readonly string[] Admin = [Roles.Admin];
    private static readonly string[] AdminVendedor = [Roles.Admin, Roles.Vendedor];
    private static readonly string[] AdminConta = [Roles.Admin, Roles.Conta];
    private static readonly string[] AdminVendedorConta = [Roles.Admin, Roles.Vendedor, Roles.Conta];
    private static readonly string[] AdminAlmacen = [Roles.Admin, Roles.Almacen];
    private static readonly string[] Todos = [Roles.Admin, Roles.Vendedor, Roles.Almacen, Roles.Conta];

    public static IReadOnlyList<PermisoPagina> Todas { get; } =
    [
        new("inicio", "Inicio", Todos),
        new("ventas-pos", "Punto de venta", AdminVendedor),
        new("facturacion", "Facturación CFDI", AdminVendedorConta),
        new("historial-ventas", "Historial de ventas", AdminVendedorConta),
        new("notas-credito", "Notas de crédito", AdminVendedorConta),
        new("complementos-pago", "Complementos de pago", AdminConta),
        new("corte-caja", "Corte de caja", AdminVendedor),
        new("cotizaciones", "Cotizaciones y órdenes de venta", AdminVendedor),
        new("metas", "Metas y comisiones", AdminVendedor),
        new("ventas-gestion", "Dashboard de gestión", AdminConta),
        new("reportes", "Reportes financieros", AdminConta),
        new("estado-cuentas", "Cuentas por cobrar", AdminConta),
        new("bancos", "Conciliación bancaria", AdminConta),
        new("gastos", "Gastos operativos", AdminVendedorConta),
        new("comunicados", "Comunicados", AdminVendedorConta),
        new("productos", "Productos", AdminAlmacen),
        new("almacen", "Inventario y stock", AdminAlmacen),
        new("viscosidad", "Aceites por viscosidad", AdminAlmacen),
        new("compras", "Compras", AdminAlmacen),
        new("clientes", "Clientes", AdminVendedor),
        new("proveedores", "Proveedores", AdminVendedor),
        new("configuracion", "Configuración", Admin),
        new("sepomex", "SEPOMEX", Admin),
        new("sucursal", "Datos de sucursal", Admin),
        new("usuarios", "Usuarios", Admin),
        new("auditoria", "Auditoría", Admin),
        new("ticket", "Ticket de venta", AdminVendedorConta)
    ];

    public static string Nombre(string ruta) => $"pagina:{ruta}:{Acceder}";
}

namespace OilsMexico.Application.DTOs;

/// <summary>
/// DTOs de catálogos maestros (lecturas y CRUD liviano) que antes se consultaban
/// directo contra <c>ErpDbContext</c> desde las páginas Blazor. Al vivir en la capa
/// de aplicación, la capa web depende de abstracciones (DIP) y no del ORM.
/// </summary>
public sealed record SucursalDto(
    int Id, string Nombre, string CodigoSucursal, string? Direccion, string? RfcEmisor,
    bool Activa, string? RazonSocial, string? RegimenFiscal, string? CodigoPostal,
    string? Calle, string? NumeroExterior, string? NumeroInterior, string? Colonia,
    string? Municipio, string? Estado, string? Ciudad, string? Pais,
    string? Telefono, string? Email);

/// <summary>Renglón del combo de vendedores (id + nombre) para asignaciones en POS/Clientes.</summary>
public sealed record VendedorDto(int Id, string Nombre);

/// <summary>Producto del catálogo maestro tal como lo edita la página Productos.</summary>
public sealed record ProductoCatalogoDto(
    int Id, string Sku, string Nombre, string Marca, string Viscosidad,
    string TipoBase, string? DescripcionTecnica, string? Categoria, string? SkuAnterior,
    string? Sae, string? Especificacion, int PiezasPorCaja, decimal PrecioLista,
    decimal PrecioLpOroConIva, decimal PrecioUnitario, decimal PrecioVenta,
    decimal PrecioMayoreo, decimal PrecioCosto, bool Activo);

/// <summary>Presentación (unidad de venta) de un producto, con su factor de conversión a litros.</summary>
public sealed record UnidadMedidaDto(
    int Id, int ProductoId, string UnidadNombre, decimal FactorConversion,
    string? CodigoBarra, decimal PrecioUnitario);

/// <summary>Stock agregado de un producto por sucursal (para la vista de detalle de Productos).</summary>
public sealed record StockSucursalDto(string Sucursal, decimal Stock, decimal Minimo, bool BajoMinimo);

/// <summary>Usuario del sistema tal como lo edita la página Usuarios.</summary>
public sealed record UsuarioDto(
    int Id, string Nombre, string? Correo, string PinHash, string PasswordHash,
    string Rol, int? RoleId, int SucursalId, bool Activo, int IntentosFallidos,
    int BloqueosTemporales, DateTime? BloqueadoHastaUtc, bool BloqueadoDefinitivamente,
    string? SesionToken, bool DosFaActivo, string? DosFaSecret, DateTime? UltimoCambioPasswordUtc);

/// <summary>Renglón de la lista de usuarios (id + nombre) para filtros y asignaciones.</summary>
public sealed record UsuarioRowDto(
    int Id, string Nombre, string Rol, string Sucursal, bool Activo,
    bool BloqueadoDefinitivo, bool BloqueadoTemporal);

/// <summary>Sucursal resumida (id + nombre + código) para combos y validaciones.</summary>
public sealed record SucursalResumenDto(int Id, string Nombre, string CodigoSucursal);

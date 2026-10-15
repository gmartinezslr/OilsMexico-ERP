namespace OilsMexico.Application.DTOs;

/// <summary>
/// DTOs de catálogos maestros (lecturas y CRUD liviano) que antes se consultaban
/// directo contra <c>ErpDbContext</c> desde las páginas Blazor. Al vivir en la capa
/// de aplicación, la capa web depende de abstracciones (DIP) y no del ORM.
/// </summary>
/// <summary>
/// Sucursal tal como la edita la página Datos de la sucursal. Registro mutable
/// (propiedades con <c>set</c>): la página lo enlaza con <c>@bind</c> y normaliza
/// campos antes de enviarlo al servicio.
/// </summary>
public sealed record SucursalDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CodigoSucursal { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? RfcEmisor { get; set; }
    public bool Activa { get; set; } = true;
    public string? RazonSocial { get; set; }
    public string? RegimenFiscal { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string? Pais { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}

/// <summary>Renglón del combo de vendedores (id + nombre) para asignaciones en POS/Clientes.</summary>
public sealed record VendedorDto(int Id, string Nombre);

/// <summary>
/// Producto del catálogo maestro tal como lo edita la página Productos.
/// Registro mutable (<c>@bind</c> en la página + normalización antes de guardar).
/// </summary>
public sealed record ProductoCatalogoDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Viscosidad { get; set; } = string.Empty;
    public string TipoBase { get; set; } = "Sintetico";
    public string? DescripcionTecnica { get; set; }
    public string? Categoria { get; set; }
    public string? SkuAnterior { get; set; }
    public string? Sae { get; set; }
    public string? Especificacion { get; set; }
    public int PiezasPorCaja { get; set; } = 1;
    public decimal PrecioLista { get; set; }
    public decimal PrecioLpOroConIva { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal PrecioMayoreo { get; set; }
    public decimal PrecioCosto { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Presentación (unidad de venta) de un producto, con su factor de conversión a litros.</summary>
public sealed record UnidadMedidaDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string UnidadNombre { get; set; } = string.Empty;
    public decimal FactorConversion { get; set; } = 1m;
    public string? CodigoBarra { get; set; }
    public decimal PrecioUnitario { get; set; }
}

/// <summary>Stock agregado de un producto por sucursal (para la vista de detalle de Productos).</summary>
public sealed record StockSucursalDto(string Sucursal, decimal Stock, decimal Minimo, bool BajoMinimo);

/// <summary>
/// Usuario del sistema tal como lo edita la página Usuarios. Registro mutable
/// (<c>@bind</c> + normalización de contraseña/correo antes de guardar).
/// </summary>
public sealed record UsuarioDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Correo { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = Domain.Services.Roles.Vendedor;
    public int? RoleId { get; set; }
    public int SucursalId { get; set; }
    public bool Activo { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public int BloqueosTemporales { get; set; }
    public DateTime? BloqueadoHastaUtc { get; set; }
    public bool BloqueadoDefinitivamente { get; set; }
    public string? SesionToken { get; set; }
    public bool DosFaActivo { get; set; }
    public string? DosFaSecret { get; set; }
    public DateTime? UltimoCambioPasswordUtc { get; set; }
}

/// <summary>Renglón de la lista de usuarios (id + nombre) para filtros y asignaciones.</summary>
public sealed record UsuarioRowDto(
    int Id, string Nombre, string Rol, string Sucursal, bool Activo,
    bool BloqueadoDefinitivo, bool BloqueadoTemporal);

/// <summary>Sucursal resumida (id + nombre + código) para combos y validaciones.</summary>
public sealed record SucursalResumenDto(int Id, string Nombre, string CodigoSucursal);

/// <summary>
/// Cliente tal como lo edita la página Clientes (wizard 3 pasos). Registro mutable
/// (<c>@bind</c>); el servicio sólo persiste — las validaciones de RFC/situación fiscal
/// y domicilio SEPOMEX viven en la página como feedback en vivo.
/// </summary>
public sealed record ClienteCatalogoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Rfc { get; set; } = "XAXX010101000";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string Pais { get; set; } = "México";
    public bool Activo { get; set; } = true;
    public int DiasCredito { get; set; }
    public decimal LimiteCredito { get; set; }
    public string TipoPrecio { get; set; } = "menudeo";
    public string RegimenFiscal { get; set; } = "616";
    public string CodigoPostal { get; set; } = "06600";
    public int? VendedorId { get; set; }
    public Domain.Enums.SituacionFiscal SituacionFiscal { get; set; } = Domain.Enums.SituacionFiscal.Actual;
}

/// <summary>
/// Proveedor tal como lo edita la página Proveedores (wizard 3 pasos). Registro mutable;
/// el servicio sólo persiste (validaciones RFC/situación/SEPOMEX en la página).
/// </summary>
public sealed record ProveedorCatalogoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Rfc { get; set; } = "XAXX010101000";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string CodigoPostal { get; set; } = string.Empty;
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? Ciudad { get; set; }
    public string Pais { get; set; } = "México";
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
    public int DiasCredito { get; set; } = 30;
    public Domain.Enums.SituacionFiscal SituacionFiscal { get; set; } = Domain.Enums.SituacionFiscal.Actual;
}

namespace OilsMexico.Application.DTOs;

public sealed record ListaPrecioDto(int Id, string Nombre, string TipoCliente, bool Activa);
public sealed record PrecioVolumenDto(int Id, int ListaPrecioId, int ProductoId, string Producto,
    decimal CantidadMinima, decimal PrecioUnitario);
public sealed record PrecioResueltoDto(decimal Precio, string Origen);

public sealed record CompatibilidadVehiculoRequest(string Marca, string Modelo, int AnioDesde,
    int AnioHasta, string Motor, string Viscosidad, string? Norma, decimal? CapacidadLitros, int? ProductoId);
public sealed record CompatibilidadVehiculoDto(int Id, string Marca, string Modelo, int AnioDesde,
    int AnioHasta, string Motor, string Viscosidad, string? Norma, decimal? CapacidadLitros,
    int? ProductoId, string? Producto);

public sealed record EvaluacionProveedorRequest(int CompraId, int Cumplimiento, int Puntualidad, string? Comentarios);
public sealed record EvaluacionProveedorDto(int CompraId, string Proveedor, int Cumplimiento,
    int Puntualidad, decimal Promedio, string? Comentarios, DateTime FechaUtc);

public sealed record EntregaCrearRequest(int OrdenVentaId, string Ruta, string Chofer,
    string Vehiculo, string? Placas, DateTime FechaProgramada);
public sealed record EntregaDetalleDto(int ProductoId, string Producto, decimal Programada,
    decimal Entregada, decimal Devuelta);
public sealed record EntregaLiquidarRenglonDto(int ProductoId, decimal Entregada, decimal Devuelta);
public sealed record EntregaLiquidarRequest(int EntregaId, List<EntregaLiquidarRenglonDto> Renglones);
public sealed record EntregaRepartoDto(int Id, int OrdenVentaId, string FolioOrden, string Cliente,
    string Ruta, string Chofer, string Vehiculo, string? Placas, string Estado, DateTime FechaProgramada,
    List<EntregaDetalleDto> Detalles, bool TieneCartaPorte);

public sealed record ControlTamborDto(int Id, int ProductoId, string Producto, decimal LitrosPorTambor,
    int TamboresCerrados, decimal LitrosAbiertos);
public sealed record EnvaseMovimientoRequest(int ClienteId, string Envase, int Cantidad,
    decimal DepositoUnitario, bool Devolucion, string? Referencia);
public sealed record EnvaseSaldoDto(int ClienteId, string Cliente, string Envase, int Saldo,
    decimal DepositoPendiente);
public sealed record CartaPorteRequest(int EntregaId, string RfcOperador, string PermisoSct,
    string NumeroPermisoSct, string ConfiguracionVehicular, string Placas, decimal PesoBrutoKg,
    string CodigoPostalOrigen, string CodigoPostalDestino);
public sealed record CartaPorteDto(int EntregaId, string RfcOperador, string PermisoSct,
    string NumeroPermisoSct, string ConfiguracionVehicular, string Placas, decimal PesoBrutoKg,
    string CodigoPostalOrigen, string CodigoPostalDestino, string XmlBorrador);

namespace OilsMexico.Domain.Entities;

public sealed class ListaPrecio
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string TipoCliente { get; set; } = "menudeo";
    public bool Activa { get; set; } = true;
    public List<PrecioVolumen> Precios { get; set; } = [];
}

public sealed class PrecioVolumen
{
    public int Id { get; set; }
    public int ListaPrecioId { get; set; }
    public int ProductoId { get; set; }
    public decimal CantidadMinima { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public ListaPrecio? ListaPrecio { get; set; }
}

public sealed class CompatibilidadVehiculo
{
    public int Id { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public int AnioDesde { get; set; }
    public int AnioHasta { get; set; }
    public string Motor { get; set; } = string.Empty;
    public string Viscosidad { get; set; } = string.Empty;
    public string? Norma { get; set; }
    public decimal? CapacidadLitros { get; set; }
    public int? ProductoId { get; set; }
}

public sealed class EvaluacionProveedor
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public int ProveedorId { get; set; }
    public int UsuarioId { get; set; }
    public int Cumplimiento { get; set; }
    public int Puntualidad { get; set; }
    public string? Comentarios { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EntregaReparto
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int OrdenVentaId { get; set; }
    public int UsuarioId { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public string Chofer { get; set; } = string.Empty;
    public string Vehiculo { get; set; } = string.Empty;
    public string? Placas { get; set; }
    public string Estado { get; set; } = "Programada";
    public DateTime FechaProgramada { get; set; }
    public DateTime? FechaLiquidacion { get; set; }
    public List<EntregaRepartoDetalle> Detalles { get; set; } = [];
    public BorradorCartaPorte? CartaPorte { get; set; }
}

public sealed class EntregaRepartoDetalle
{
    public int Id { get; set; }
    public int EntregaRepartoId { get; set; }
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public decimal CantidadProgramada { get; set; }
    public decimal CantidadEntregada { get; set; }
    public decimal CantidadDevuelta { get; set; }
    public EntregaReparto? Entrega { get; set; }
}

public sealed class ControlTambor
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int ProductoId { get; set; }
    public decimal LitrosPorTambor { get; set; }
    public int TamboresCerrados { get; set; }
    public decimal LitrosAbiertos { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MovimientoEnvaseRetornable
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int ClienteId { get; set; }
    public string Envase { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal DepositoUnitario { get; set; }
    public string Tipo { get; set; } = "Entregado";
    public string? Referencia { get; set; }
    public int UsuarioId { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BorradorCartaPorte
{
    public int Id { get; set; }
    public int EntregaRepartoId { get; set; }
    public string RfcOperador { get; set; } = string.Empty;
    public string PermisoSct { get; set; } = string.Empty;
    public string NumeroPermisoSct { get; set; } = string.Empty;
    public string ConfiguracionVehicular { get; set; } = string.Empty;
    public string Placas { get; set; } = string.Empty;
    public decimal PesoBrutoKg { get; set; }
    public string CodigoPostalOrigen { get; set; } = string.Empty;
    public string CodigoPostalDestino { get; set; } = string.Empty;
    public string? XmlBorrador { get; set; }
    public DateTime ActualizadoUtc { get; set; } = DateTime.UtcNow;
}

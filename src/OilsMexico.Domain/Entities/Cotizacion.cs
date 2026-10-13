using OilsMexico.Domain.Enums;

namespace OilsMexico.Domain.Entities;

/// <summary>Encabezado de cotización de venta (compromiso de precio, pre-venta).</summary>
public sealed class Cotizacion
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? VendedorId { get; set; }
    public Usuario? Vendedor { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public DateTime? FechaVencimiento { get; set; } // null = sin validez; al vencer se marca Expirada
    public string? FolioInterno { get; set; } = $"CT-{0}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    public string? Notas { get; set; }
    public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Borrador;
    public int UsuarioId { get; set; }
    public List<CotizacionDetalle> Detalles { get; set; } = [];
    public List<OrdenVenta> OrdenesVenta { get; set; } = [];

    /// <summary>Totales desnormalizados (se recalculan al crear/editar desde los renglones).</summary>
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
}

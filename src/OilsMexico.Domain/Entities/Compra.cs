namespace OilsMexico.Domain.Entities;

/// <summary>Orden de compra a proveedor (encabezado). Tabla: compras.</summary>
public sealed class Compra
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public string FolioInterno { get; set; } = string.Empty; // C-{suc}-{yyyyMMddHHmmss}
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    /// <summary>Folio/factura del proveedor (para conciliar CxP).</summary>
    public string? FolioProveedor { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    /// <summary>Borrador | Recibida | Parcial | Cancelada.</summary>
    public string Estado { get; set; } = "Borrador";
    /// <summary>Pagada | Parcial | Pendiente (cuentas por pagar).</summary>
    public string EstadoPago { get; set; } = "Pendiente";
    public decimal MontoPagado { get; set; }
    public string? Notas { get; set; }
    public int UsuarioId { get; set; }
    public List<CompraDetalle> Detalles { get; set; } = [];
}

/// <summary>Renglón de orden de compra. Tabla: compra_detalle.</summary>
public sealed class CompraDetalle
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public Compra? Compra { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public int? UnidadMedidaId { get; set; }
    public string UnidadNombre { get; set; } = "Litro";
    public decimal Cantidad { get; set; }          // en unidades de compra
    public decimal FactorConversion { get; set; } = 1m;
    public decimal Litros => Cantidad * FactorConversion;
    public decimal CantidadRecibida { get; set; }  // en unidades de compra (acumulado)
    public decimal LitrosRecibidos { get; set; }   // acumulado en litros
    public decimal CostoUnitario { get; set; }     // costo por unidad de compra (sin IVA)
    public decimal Importe => Cantidad * CostoUnitario;
    public string? NumeroLote { get; set; }
    public DateOnly? FechaCaducidad { get; set; }
}

/// <summary>Pago a proveedor contra una compra (CxP). Tabla: compra_pagos.</summary>
public sealed class CompraPago
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public Compra? Compra { get; set; }
    public decimal Monto { get; set; }
    public string FormaPago { get; set; } = "03"; // catálogo SAT c_FormaPago
    public string? Referencia { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public int UsuarioId { get; set; }
}

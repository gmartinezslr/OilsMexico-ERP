namespace OilsMexico.Application.DTOs;

/// <summary>Resumen del dashboard de gestión de una sucursal.</summary>
public sealed record DashboardGestionDto(
    decimal Ingresos, decimal Iva, decimal TotalVentas, int NumVentas, decimal PromedioFactura,
    decimal UnidadesVendidas, decimal MontoCosto, decimal Utilidad, decimal VentasPorDia, int CortesAbiertos);

/// <summary>Ventas totales agrupadas por día.</summary>
public sealed record VentasPorDiaDto(DateTime Fecha, decimal Total, int Cantidad);

/// <summary>Ventas totales agrupadas por producto.</summary>
public sealed record VentasPorProductoDto(int ProductoId, decimal Cantidad, decimal Importe);

/// <summary>Turno de ventas agrupadas por marca de producto.</summary>
public sealed record MarcaProductoDto(string Marca, decimal Cantidad, decimal Importe);

/// <summary>Rotación ABC por producto.</summary>
public sealed record RotacionABCDto(int ProductoId, string Nombre, decimal Cantidad, decimal Importe, decimal PorcentajeAcumulado, string Clase);

/// <summary>Ventas totales por sucursal.</summary>
public sealed record CorteSucursalDto(int SucursalId, string Nombre, decimal TotalVentas, int NumVentas);

/// <summary>Inventario en stock por viscosidad de producto.</summary>
public sealed record ViscosidadDto(string Viscosidad, decimal Stock, int Productos);

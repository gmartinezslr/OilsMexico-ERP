using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public interface IAlmacenConsulta
{
    Task<List<AlmacenStockDto>> StockPorSucursalAsync(int sucursalId, string? filtro, CancellationToken ct = default);
    Task<List<KardexDto>> KardexAsync(int sucursalId, int productoId, int dias = 30, CancellationToken ct = default);

    /// <summary>Catálogo activo con stock de la sucursal agregado por producto (todos sus lotes), para reporte por viscosidad.</summary>
    Task<List<ViscosidadProductoDto>> ViscosidadAsync(int sucursalId, string? filtro = null, CancellationToken ct = default);
}

public sealed record AlmacenStockDto(
    int ProductoId, string Sku, string Nombre, string Marca, string Viscosidad,
    int? LoteId, string? Lote, decimal Stock, decimal Minimo, bool BajoMinimo);

/// <summary>Renglón del reporte "Aceites por viscosidad": stock en litros sumado por producto.</summary>
public sealed record ViscosidadProductoDto(
    int ProductoId, string Sku, string Nombre, string Marca, string TipoBase, string Viscosidad,
    decimal StockLitros, decimal StockMinimo, decimal PrecioVenta, bool BajoMinimo)
{
    public decimal ValorStock => StockLitros * PrecioVenta;
}

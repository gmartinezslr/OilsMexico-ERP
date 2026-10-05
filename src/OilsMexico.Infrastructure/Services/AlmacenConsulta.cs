using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public interface IAlmacenConsulta
{
    Task<List<AlmacenStockDto>> StockPorSucursalAsync(int sucursalId, string? filtro, CancellationToken ct = default);
    Task<List<KardexDto>> KardexAsync(int sucursalId, int productoId, int dias = 30, CancellationToken ct = default);
}

public sealed record AlmacenStockDto(
    int ProductoId, string Sku, string Nombre, string Marca, string Viscosidad,
    int? LoteId, string? Lote, decimal Stock, decimal Minimo, bool BajoMinimo);

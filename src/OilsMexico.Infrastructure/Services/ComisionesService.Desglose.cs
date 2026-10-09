using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Aplicación del tabulador por presentación y estado intermedio del cálculo (fase 3).
/// </summary>
public sealed partial class ComisionesService
{
    /// <summary>Llave del tabulador: el % es por producto Y por presentación (un tambor no comisiona igual que un litro).</summary>
    private readonly record struct LlaveTabulador(int ProductoId, string Unidad);

    /// <summary>% vigentes de una presentación. <c>Configurado</c> distingue «no comisiona» de «falta capturar».</summary>
    private readonly record struct Tabulador(decimal Base, decimal Bono, bool Configurado);

    /// <summary>
    /// Estado intermedio del cálculo. Lo consume tanto la vista en vivo como el cierre congelado
    /// para garantizar que ambos partan del mismo número.
    /// </summary>
    private sealed record ResultadoCalculo(
        CobranzaResumenDto Cobranza,
        CuotaVendedor? Cuota,
        List<ComisionProductoDto> Productos,
        List<string> SinTabulador,
        decimal PctDinero, decimal PctLitros,
        bool CumplioMeta, string DetalleEvaluacion,
        decimal ComisionBase, decimal ComisionBono, decimal ComisionFinal, bool AplicoPagoMinimo);

    /// <summary>
    /// Trae sólo las presentaciones de los productos que aparecieron en la cobranza del periodo
    /// (una query, no una por producto: el pseudocódigo del documento original tenía N+1).
    /// </summary>
    private async Task<Dictionary<LlaveTabulador, Tabulador>> CargarTabuladorAsync(
        List<CobranzaFacturaDto> facturas, CancellationToken ct)
    {
        var productoIds = facturas.SelectMany(f => f.Renglones)
            .Select(r => r.ProductoId).Distinct().ToList();
        if (productoIds.Count == 0) return new Dictionary<LlaveTabulador, Tabulador>();

        var filas = await db.UnidadesMedida.AsNoTracking()
            .Where(u => productoIds.Contains(u.ProductoId))
            .Select(u => new { u.ProductoId, u.UnidadNombre, u.PorcComisionBase, u.PorcComisionBono })
            .ToListAsync(ct);

        return filas.ToDictionary(
            x => new LlaveTabulador(x.ProductoId, x.UnidadNombre),
            x => new Tabulador(x.PorcComisionBase, x.PorcComisionBono,
                x.PorcComisionBase > 0 || x.PorcComisionBono > 0));
    }

    /// <summary>
    /// Multiplica el neto cobrado (ya sin IVA y prorrateado) por el % que toca.
    /// Acumula la comisión base y la de bono por separado para poder mostrar ambas cifras.
    /// </summary>
    private (List<ComisionProductoDto> Productos, List<string> SinTabulador) AplicarTabulador(
        List<CobranzaFacturaDto> facturas,
        Dictionary<LlaveTabulador, Tabulador> tabulador,
        out decimal comBase, out decimal comBono)
    {
        // Se agrupa por producto+unidad porque el mismo SKU puede venir en varias facturas
        // (y en varias presentaciones) dentro del mismo mes.
        var porProducto = facturas
            .SelectMany(f => f.Renglones)
            .GroupBy(r => new LlaveTabulador(r.ProductoId, r.Unidad))
            .Select(g =>
            {
                var neto = Redondear(g.Sum(r => r.NetoSinIvaCobrado));
                var litros = Redondear(g.Sum(r => r.LitrosCobrados));
                var (pBase, pBono, configurado) = tabulador.TryGetValue(g.Key, out var t)
                    ? t
                    : new Tabulador(0m, 0m, false);
                return new
                {
                    g.Key.ProductoId, Producto = g.First().Producto, Unidad = g.First().Unidad,
                    Neto = neto, Litros = litros, PBase = pBase, PBono = pBono, Configurado = configurado
                };
            })
            .OrderByDescending(x => x.Neto)
            .ToList();

        comBase = 0m;
        comBono = 0m;
        var sinTabulador = new List<string>();
        var productos = new List<ComisionProductoDto>(porProducto.Count);

        foreach (var p in porProducto)
        {
            var cBase = Redondear(p.Neto * p.PBase / 100m);
            var cBono = Redondear(p.Neto * p.PBono / 100m);
            comBase += cBase;
            comBono += cBono;

            // Visibilidad: si se cobró un producto que no comisiona, el vendedor pierde dinero
            // sin saber por qué. Se reporta en vez de absorberlo en silencio.
            if (!p.Configurado && p.Neto > 0)
                sinTabulador.Add($"{p.Producto} ({p.Unidad})");

            // El % «aplicado» concreto lo decide EvaluarMetas; aquí se muestra el mayor (el caso
            // normal al cumplir) y el base en caso contrario.
            var aplicado = Math.Max(p.PBase, p.PBono);
            productos.Add(new ComisionProductoDto(
                p.ProductoId, p.Producto, p.Unidad, p.Neto, p.Litros,
                p.PBase, p.PBono, aplicado, cBase, cBono));
        }

        comBase = Redondear(comBase);
        comBono = Redondear(comBono);
        return (productos, sinTabulador);
    }
}

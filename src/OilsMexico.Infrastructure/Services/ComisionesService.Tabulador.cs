using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Tabulador de comisiones por presentación (fase 3).</summary>
public sealed partial class ComisionesService
{
    public async Task<List<TabuladorDto>> ListarTabuladorAsync(int? productoId, CancellationToken ct = default)
    {
        // Los % de comisión revelan el margen del negocio: es configuración de Admin, no dato
        // que un vendedor deba poder enumerar producto por producto.
        ExigirAdmin("ver el tabulador de comisiones");
        var q = db.UnidadesMedida.AsNoTracking().Include(u => u.Producto).AsQueryable();
        if (productoId is not null) q = q.Where(u => u.ProductoId == productoId);

        var filas = await q.OrderBy(u => u.Producto.Sku).ThenBy(u => u.UnidadNombre).ToListAsync(ct);
        return filas.Select(u => new TabuladorDto(
            u.Id, u.ProductoId, u.Producto.Sku, u.Producto.Nombre, u.UnidadNombre,
            u.FactorConversion, u.PorcComisionBase, u.PorcComisionBono)).ToList();
    }

    public async Task GuardarTabuladorAsync(GuardarTabuladorRequest req, CancellationToken ct = default)
    {
        ExigirAdmin("modificar el tabulador de comisiones");
        if (req.PorcBase is < 0 or > 100 || req.PorcBono is < 0 or > 100)
            throw new InvalidOperationException("Los porcentajes deben estar entre 0 y 100 (son %, no fracciones).");
        // Invariante del incentivo: cumplir la meta jamás debe pagar menos que no cumplirla.
        if (req.PorcBono < req.PorcBase)
            throw new InvalidOperationException(
                $"El % de bono ({req.PorcBono}%) no puede ser menor al % base ({req.PorcBase}%): " +
                "cumplir la meta pagaría menos que no cumplirla.");

        var u = await db.UnidadesMedida.FirstOrDefaultAsync(x => x.Id == req.UnidadMedidaId, ct)
            ?? throw new InvalidOperationException($"La presentación {req.UnidadMedidaId} no existe.");

        u.PorcComisionBase = req.PorcBase;
        u.PorcComisionBono = req.PorcBono;
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Provee catálogos SAT y del sistema desde la tabla 'catalogos_sat' (T-13 / §6.2 / A3).
/// Alimenta combos de la UI, valida claves y agrupa formas de pago sin tocar código (OCP).
/// </summary>
public sealed class CatalogosSatService : ICatalogosSatService
{
    private readonly ErpDbContext? _db;

    public CatalogosSatService(ErpDbContext db)
    {
        _db = db;
    }

    /// <summary>Constructor sin DbContext para pruebas unitarias en memoria o prerender.</summary>
    public CatalogosSatService()
    {
        _db = null;
    }

    public CatalogosSatDto Obtener()
    {
        if (_db is null)
        {
            return ConstruirDtoDesdeSemilla(CatalogosSatSeed.Semilla);
        }

        try
        {
            var registros = _db.CatalogosSat.AsNoTracking()
                .Where(c => c.Activo)
                .OrderBy(c => c.Orden)
                .ToList();

            if (registros.Count == 0)
            {
                return ConstruirDtoDesdeSemilla(CatalogosSatSeed.Semilla);
            }

            return ConstruirDto(registros);
        }
        catch
        {
            return ConstruirDtoDesdeSemilla(CatalogosSatSeed.Semilla);
        }
    }

    public async Task<CatalogosSatDto> ObtenerAsync(CancellationToken ct = default)
    {
        if (_db is null)
        {
            return ConstruirDtoDesdeSemilla(CatalogosSatSeed.Semilla);
        }

        try
        {
            var registros = await _db.CatalogosSat.AsNoTracking()
                .Where(c => c.Activo)
                .OrderBy(c => c.Orden)
                .ToListAsync(ct);

            if (registros.Count == 0)
            {
                return ConstruirDtoDesdeSemilla(CatalogosSatSeed.Semilla);
            }

            return ConstruirDto(registros);
        }
        catch
        {
            return ConstruirDtoDesdeSemilla(CatalogosSatSeed.Semilla);
        }
    }

    public string ObtenerGrupoFormaPago(string formaPagoSat)
    {
        if (string.IsNullOrWhiteSpace(formaPagoSat)) return "otro";
        var dto = Obtener();
        if (dto.GruposFormasPago.TryGetValue(formaPagoSat, out var grupo) && !string.IsNullOrWhiteSpace(grupo))
        {
            return grupo.ToLowerInvariant();
        }

        return formaPagoSat switch
        {
            "01" => "efectivo",
            "04" or "28" => "tarjeta",
            "03" => "transferencia",
            _ => "otro"
        };
    }

    public async Task<string> ObtenerGrupoFormaPagoAsync(string formaPagoSat, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(formaPagoSat)) return "otro";
        var dto = await ObtenerAsync(ct);
        if (dto.GruposFormasPago.TryGetValue(formaPagoSat, out var grupo) && !string.IsNullOrWhiteSpace(grupo))
        {
            return grupo.ToLowerInvariant();
        }

        return formaPagoSat switch
        {
            "01" => "efectivo",
            "04" or "28" => "tarjeta",
            "03" => "transferencia",
            _ => "otro"
        };
    }

    private static CatalogosSatDto ConstruirDto(List<CatalogoSat> lista)
    {
        var formas = new Dictionary<string, string>();
        var metodos = new Dictionary<string, string>();
        var usos = new Dictionary<string, string>();
        var regimenes = new Dictionary<string, string>();
        var motivos = new Dictionary<string, string>();
        var situaciones = new Dictionary<string, string>();
        var grupos = new Dictionary<string, string>();

        foreach (var item in lista)
        {
            switch (item.Catalogo)
            {
                case "c_FormaPago":
                    formas[item.Clave] = item.Descripcion;
                    if (!string.IsNullOrEmpty(item.Grupo)) grupos[item.Clave] = item.Grupo;
                    break;
                case "c_MetodoPago":
                    metodos[item.Clave] = item.Descripcion;
                    break;
                case "c_UsoCFDI":
                    usos[item.Clave] = item.Descripcion;
                    break;
                case "c_RegimenFiscal":
                    regimenes[item.Clave] = item.Descripcion;
                    break;
                case "c_MotivoCancelacion":
                    motivos[item.Clave] = item.Descripcion;
                    break;
                case "c_SituacionFiscal":
                    situaciones[item.Clave] = item.Descripcion;
                    break;
            }
        }

        return new CatalogosSatDto(formas, metodos, usos, regimenes, motivos, situaciones, grupos);
    }

    private static CatalogosSatDto ConstruirDtoDesdeSemilla(IEnumerable<(string Catalogo, string Clave, string Descripcion, string? Grupo, int Orden)> semilla)
    {
        var formas = new Dictionary<string, string>();
        var metodos = new Dictionary<string, string>();
        var usos = new Dictionary<string, string>();
        var regimenes = new Dictionary<string, string>();
        var motivos = new Dictionary<string, string>();
        var situaciones = new Dictionary<string, string>();
        var grupos = new Dictionary<string, string>();

        foreach (var (cat, clave, desc, grupo, _) in semilla)
        {
            switch (cat)
            {
                case "c_FormaPago":
                    formas[clave] = desc;
                    if (!string.IsNullOrEmpty(grupo)) grupos[clave] = grupo;
                    break;
                case "c_MetodoPago":
                    metodos[clave] = desc;
                    break;
                case "c_UsoCFDI":
                    usos[clave] = desc;
                    break;
                case "c_RegimenFiscal":
                    regimenes[clave] = desc;
                    break;
                case "c_MotivoCancelacion":
                    motivos[clave] = desc;
                    break;
                case "c_SituacionFiscal":
                    situaciones[clave] = desc;
                    break;
            }
        }

        return new CatalogosSatDto(formas, metodos, usos, regimenes, motivos, situaciones, grupos);
    }
}

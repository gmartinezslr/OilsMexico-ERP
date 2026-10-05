using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class SepomexService
{
    public async Task<SepomexImportResult> ImportarAsync(Stream archivo, bool reemplazar, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        List<string> datos;
        char delim;
        if (SepomexParser.EsExcel(bytes))
        {
            // Excel oficial (.xls/.xlsx): una hoja por estado; LeerExcel ya filtra
            // encabezados/hojas auxiliares y une las 15 columnas con '|'.
            ms.Position = 0;
            datos = SepomexParser.LeerExcel(ms, ct);
            delim = '|';
        }
        else
        {
            var texto = SepomexParser.Decodificar(bytes);
            datos = texto.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            delim = datos.Count == 0 ? '|' : SepomexParser.DetectarDelimitador(datos.Take(5).ToList());
        }
        if (datos.Count == 0)
            return new SepomexImportResult(0, 0, 0, 0, reemplazar ? "reemplazo" : "agregado");

        var inicio = delim == '|' && SepomexParser.EsExcel(bytes)
            ? 0 // las filas de encabezado del Excel ya se descartaron en LeerExcel
            : SepomexParser.EsEncabezado(datos[0], delim) ? 1 : 0;

        if (reemplazar)
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE codigos_postales RESTART IDENTITY", ct);

        var lote = new List<CodigoPostal>(capacity: 4000);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int leidas = 0, insertadas = 0, omitidas = 0;

        foreach (var raw in datos.Skip(inicio))
        {
            ct.ThrowIfCancellationRequested();
            var ent = SepomexParser.ParseLinea(raw, delim);
            if (ent is null) { omitidas++; continue; }
            var clave = ent.Codigo + "|" + (ent.AsentamientoId ?? "") + "|" + ent.Asentamiento.ToUpperInvariant();
            if (!vistos.Add(clave)) { omitidas++; continue; }
            leidas++;
            lote.Add(ent);
            if (lote.Count >= 4000)
            {
                var (ins, om) = await GuardarLoteAsync(lote, ct);
                insertadas += ins; omitidas += om;
                lote.Clear();
            }
        }
        if (lote.Count > 0)
        {
            var (ins, om) = await GuardarLoteAsync(lote, ct);
            insertadas += ins; omitidas += om;
        }
        return new SepomexImportResult(leidas, insertadas, omitidas,
            reemplazar ? insertadas : 0, reemplazar ? "reemplazo" : "agregado");
    }

    private async Task<(int insertadas, int omitidas)> GuardarLoteAsync(List<CodigoPostal> lote, CancellationToken ct)
    {
        var cps = lote.Select(x => x.Codigo).Distinct().ToList();
        var existentes = await db.CodigosPostales.AsNoTracking()
            .Where(x => cps.Contains(x.Codigo))
            .Select(x => x.Codigo + "|" + (x.AsentamientoId ?? "") + "|" + x.Asentamiento.ToUpper())
            .ToListAsync(ct);
        var set = new HashSet<string>(existentes, StringComparer.OrdinalIgnoreCase);
        var nuevos = lote.Where(x => set.Add(
            x.Codigo + "|" + (x.AsentamientoId ?? "") + "|" + x.Asentamiento.ToUpperInvariant())).ToList();
        if (nuevos.Count == 0) return (0, lote.Count);
        db.CodigosPostales.AddRange(nuevos);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            int ins = 0, om = 0;
            foreach (var e in nuevos)
            {
                ct.ThrowIfCancellationRequested();
                db.CodigosPostales.Add(e);
                try { await db.SaveChangesAsync(ct); ins++; }
                catch (DbUpdateException) { db.ChangeTracker.Clear(); om++; }
            }
            return (ins, om + (lote.Count - nuevos.Count));
        }
        db.ChangeTracker.Clear();
        return (nuevos.Count, lote.Count - nuevos.Count);
    }
}

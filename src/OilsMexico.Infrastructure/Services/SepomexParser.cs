using System.Globalization;
using System.Text;
using ExcelDataReader;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Parser del TXT/CSV oficial SEPOMEX (15 columnas, '|' o ',') y del Excel oficial (.xls/.xlsx).</summary>
internal static class SepomexParser
{
    public static CodigoPostal? ParseLinea(string raw, char delim)
    {
        var p = Dividir(raw, delim);
        if (p.Length < 15) return null;
        var cp = p[0].Trim();
        if (cp.Length != 5 || !cp.All(char.IsDigit)) return null;
        if (string.IsNullOrWhiteSpace(p[1])) return null;
        return new CodigoPostal
        {
            Codigo = cp,
            Asentamiento = p[1].Trim(),
            TipoAsentamiento = p[2].Trim(),
            Municipio = p[3].Trim(),
            Estado = p[4].Trim(),
            Ciudad = Norm(p[5]),
            Dcp = Norm(p[6]),
            EstadoId = Norm(p[7]),
            Oficina = Norm(p[8]),
            Ccp = Norm(p[9]),
            TipoAsentamientoId = Norm(p[10]),
            MunicipioId = Norm(p[11]),
            AsentamientoId = Norm(p[12]),
            Zona = Norm(p[13]),
            CiudadId = Norm(p[14]),
        };
    }

    /// <summary>Detecta archivos Excel: OLE2/BIFF (.xls) o ZIP (.xlsx) por su firma binaria.</summary>
    public static bool EsExcel(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0)
            return true;
        return bytes.Length >= 4 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K';
    }

    /// <summary>
    /// Lee el Excel oficial SEPOMEX (una hoja por estado, 15 columnas con el mismo orden del TXT)
    /// y devuelve una línea por fila válida, unida con '|', lista para ParseLinea.
    /// Filtra hojas auxiliares (ej: "Nota"), filas de encabezado y filas sin CP de 5 dígitos.
    /// </summary>
    public static List<string> LeerExcel(Stream archivo, CancellationToken ct = default)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var lineas = new List<string>(capacity: 160_000);
        using var reader = ExcelReaderFactory.CreateReader(archivo);
        do
        {
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                if (reader.FieldCount < 15) continue; // hojas auxiliares (ej: "Nota")
                var cp = Celda(reader.GetValue(0)).Trim();
                if (cp.Length != 5 || !cp.All(char.IsDigit)) continue; // encabezados y filas vacías
                var sb = new StringBuilder(capacity: 256);
                for (var i = 0; i < 15; i++)
                {
                    if (i > 0) sb.Append('|');
                    sb.Append(Celda(reader.GetValue(i)).Trim());
                }
                lineas.Add(sb.ToString());
            }
        } while (reader.NextResult());
        return lineas;
    }

    /// <summary>Convierte una celda de Excel a texto estable (sin separadores culturales).</summary>
    private static string Celda(object? v) => v switch
    {
        null => string.Empty,
        double d => d % 1 == 0 && Math.Abs(d) < 1e15
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString(CultureInfo.InvariantCulture),
        float f => f % 1 == 0 ? ((long)f).ToString(CultureInfo.InvariantCulture) : f.ToString(CultureInfo.InvariantCulture),
        decimal m => m % 1 == 0 ? ((long)m).ToString(CultureInfo.InvariantCulture) : m.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => v.ToString() ?? string.Empty,
    };

    public static string Decodificar(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        var t = Encoding.UTF8.GetString(bytes);
        return t.Contains('�') ? Encoding.Latin1.GetString(bytes) : t;
    }

    public static char DetectarDelimitador(List<string> muestra)
    {
        var cands = new[] { '|', ',', ';', '\t' };
        return cands.MaxBy(d => muestra.Sum(l => l.Count(c => c == d)));
    }

    public static bool EsEncabezado(string primera, char delim)
    {
        var p = Dividir(primera, delim);
        if (p.Length < 2) return false;
        var h = (p[0] + " " + p[1]).ToLowerInvariant();
        return h.Contains("codigo") || h.Contains("código") || h.Contains("d_codigo")
            || h.Contains("asenta") || h.Contains("colonia");
    }

    public static string[] Dividir(string linea, char delim)
    {
        if (delim != ',') return linea.Split(delim);
        var partes = new List<string>();
        var act = new StringBuilder();
        bool com = false;
        foreach (var ch in linea)
        {
            if (ch == '"') { com = !com; continue; }
            if (ch == ',' && !com) { partes.Add(act.ToString()); act.Clear(); }
            else act.Append(ch);
        }
        partes.Add(act.ToString());
        return [.. partes];
    }

    private static string? Norm(string? v)
    {
        v = (v ?? "").Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }
}

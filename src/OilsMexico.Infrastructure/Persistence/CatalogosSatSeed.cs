using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Persistence;

public static class CatalogosSatSeed
{
    public static readonly (string Catalogo, string Clave, string Descripcion, string? Grupo, int Orden)[] Semilla =
    [
        // Formas de pago (c_FormaPago) con su grupo funcional para Corte de Caja (T-13 / A3 / OCP)
        ("c_FormaPago", "01", "Efectivo", "efectivo", 1),
        ("c_FormaPago", "02", "Cheque nominativo", "otro", 2),
        ("c_FormaPago", "03", "Transferencia electrónica", "transferencia", 3),
        ("c_FormaPago", "04", "Tarjeta de crédito", "tarjeta", 4),
        ("c_FormaPago", "28", "Tarjeta de débito", "tarjeta", 5),
        ("c_FormaPago", "99", "Por definir", "otro", 6),

        // Métodos de pago (c_MetodoPago)
        ("c_MetodoPago", "PUE", "Pago en una sola exhibición", null, 1),
        ("c_MetodoPago", "PPD", "Pago en parcialidades o diferido", null, 2),

        // Usos de CFDI (c_UsoCFDI)
        ("c_UsoCFDI", "G01", "Adquisición de mercancías", null, 1),
        ("c_UsoCFDI", "G03", "Gastos en general", null, 2),
        ("c_UsoCFDI", "S01", "Sin efectos fiscales", null, 3),
        ("c_UsoCFDI", "CP01", "Pagos", null, 4),

        // Regímenes fiscales (c_RegimenFiscal)
        ("c_RegimenFiscal", "601", "General de Ley Personas Morales", "moral", 1),
        ("c_RegimenFiscal", "612", "Personas Físicas con Actividades Empresariales", "fisica", 2),
        ("c_RegimenFiscal", "616", "Sin obligaciones fiscales", "fisica", 3),
        ("c_RegimenFiscal", "626", "Régimen Simplificado de Confianza", "mixto", 4),

        // Motivos de cancelación SAT (c_MotivoCancelacion)
        ("c_MotivoCancelacion", "01", "Comprobante emitido con errores con relación", "01", 1),
        ("c_MotivoCancelacion", "02", "Comprobante emitido con errores sin relación", "02", 2),
        ("c_MotivoCancelacion", "03", "No se llevó a cabo la operación", "03", 3),
        ("c_MotivoCancelacion", "04", "Operación nominativa relacionada en una factura global", "04", 4),

        // Situación fiscal ante el SAT (c_SituacionFiscal)
        ("c_SituacionFiscal", "Actual", "Actual — operaciones normales", "normal", 1),
        ("c_SituacionFiscal", "Adeudada", "Adeudada — obligaciones fiscales pendientes", "alerta", 2),
        ("c_SituacionFiscal", "Inactiva", "Inactiva — no opera en el periodo", "alerta", 3),
        ("c_SituacionFiscal", "Suspendida", "Suspendida — incumplimientos temporales", "alerta", 4),
        ("c_SituacionFiscal", "Cancelada", "Cancelada — anulada ante el SAT (no operar)", "bloqueo", 5),
        ("c_SituacionFiscal", "Baja", "Baja — baja definitiva (no operar)", "bloqueo", 6)
    ];

    public static async Task AsegurarAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var existentes = await db.CatalogosSat.AsNoTracking()
            .Select(x => x.Catalogo + ":" + x.Clave)
            .ToListAsync(ct);

        var faltantes = new List<CatalogoSat>();
        foreach (var (cat, clave, desc, grupo, orden) in Semilla)
        {
            if (!existentes.Contains(cat + ":" + clave))
            {
                faltantes.Add(new CatalogoSat
                {
                    Catalogo = cat,
                    Clave = clave,
                    Descripcion = desc,
                    Grupo = grupo,
                    Activo = true,
                    Orden = orden
                });
            }
        }

        if (faltantes.Count > 0)
        {
            db.CatalogosSat.AddRange(faltantes);
            await db.SaveChangesAsync(ct);
        }
    }
}

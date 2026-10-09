using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Pantalla B del documento: portal de avance del vendedor (con run-rate y potenciales).</summary>
public sealed partial class ComisionesService
{
    public async Task<PortalVendedorDto> PortalAsync(
        int vendedorId, int anio, int mes, int? sucursalId = null, CancellationToken ct = default)
    {
        ExigirDuenoOAdmin(vendedorId, "ver el avance");
        ValidarPeriodo(anio, mes);

        var calc = await CalcularAsync(vendedorId, anio, mes, sucursalId, ct);

        var (desde, hasta) = RangoPeriodo(anio, mes);
        var pendientes = await cobranza.PotencialesAsync(vendedorId, desde, hasta, sucursalId, ct);

        // Run-rate: se proyecta con el ritmo REAL de los días transcurridos, no con el mes
        // completo (proyectar contra 31 cuando sólo pasaron 5 subestima el avance brutalmente).
        var diasDelMes = DateTime.DaysInMonth(anio, mes);
        var hoy = DateTime.Today;
        var esMesActual = anio == hoy.Year && mes == hoy.Month;
        var dias = esMesActual
            ? Math.Max(1, hoy.Day)          // al menos 1 para no dividir entre cero
            : diasDelMes;                    // meses cerrados: el periodo ya está completo

        var proyDinero = Redondear(calc.DineroReal / dias * diasDelMes);
        var proyLitros = Redondear(calc.LitrosReales / dias * diasDelMes);

        var cierre = await db.ComisionesHistorial.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VendedorId == vendedorId && c.Anio == anio && c.Mes == mes, ct);

        return new PortalVendedorDto(
            calc,
            esMesActual ? hoy.Day : diasDelMes,
            diasDelMes,
            proyDinero, proyLitros,
            Redondear(pendientes.Sum(p => p.ComisionPotencial)),
            pendientes,
            cierre is not null && cierre.Estado != EstadoComision.Borrador,
            cierre?.Estado);
    }
}

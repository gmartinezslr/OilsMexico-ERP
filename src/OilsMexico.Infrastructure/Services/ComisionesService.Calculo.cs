using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Motor de cálculo: cobranza efectiva + metas + tabulador = comisión (fase 3).</summary>
public sealed partial class ComisionesService
{
    public async Task<ComisionCalculoDto> CalcularAsync(
        int vendedorId, int anio, int mes, int? sucursalId = null, CancellationToken ct = default)
    {
        ExigirDuenoOAdmin(vendedorId, "ver la comisión");
        ValidarPeriodo(anio, mes);
        var (r, nombreVendedor) = await CalcularInternoAsync(vendedorId, anio, mes, sucursalId, ct);

        return new ComisionCalculoDto(
            vendedorId, nombreVendedor, anio, mes,
            r.Cuota is not null,
            r.Cuota?.MetaDinero, r.Cuota?.MetaLitros,
            r.Cuota?.Operador ?? OperadorLogico.SoloDinero,
            r.Cuota?.AccionIncumplimiento ?? AccionIncumplimiento.CeroComision,
            r.Cuota?.MontoPagoMinimo ?? 0m,
            r.Cobranza.NetoCobradoSinIva, r.Cobranza.LitrosCobrados,
            r.Cobranza.CobradoBrutoConIva, r.Cobranza.FacturasCobradas,
            r.PctDinero, r.PctLitros, r.CumplioMeta, r.DetalleEvaluacion,
            r.ComisionBase, r.ComisionBono, r.ComisionFinal, r.AplicoPagoMinimo,
            r.SinTabulador, r.Productos);
    }

    /// <summary>
    /// Núcleo compartido por la vista en vivo y por el cierre. Es UN método a propósito:
    /// si la pantalla del vendedor y el cierre congelado calcularan por su cuenta, tarde o
    /// temprano mostrarían números distintos y el cierre sería inauditable.
    /// </summary>
    private async Task<(ResultadoCalculo Resultado, string Vendedor)> CalcularInternoAsync(
        int vendedorId, int anio, int mes, int? sucursalId, CancellationToken ct)
    {
        var (desde, hasta) = RangoPeriodo(anio, mes);

        // 1) La cobranza REAL del periodo la decide CobranzaService (cash basis + prorrateo
        //    + base sin IVA). Aquí no se recalcula nada de eso.
        var cobranzaResumen = await cobranza.CobranzaVendedorAsync(vendedorId, desde, hasta, sucursalId, ct);
        var facturas = await cobranza.DetalleCobranzaAsync(vendedorId, desde, hasta, sucursalId, ct);

        var nombres = await NombresVendedoresAsync([vendedorId], ct);
        var nombreVendedor = Nombre(nombres, vendedorId);

        // 2) Metas del periodo (configuración viva, NO congelada).
        var cuota = await db.CuotasVendedor.AsNoTracking().FirstOrDefaultAsync(
            c => c.VendedorId == vendedorId && c.Anio == anio && c.Mes == mes, ct);

        // 3) Tabulador indexado por (producto, unidad): la cobranza trae el nombre de la
        //    unidad, no su id, así que la llave compuesta es el join natural entre capas.
        var tabulador = await CargarTabuladorAsync(facturas, ct);
        var (productos, sinTabulador) = AplicarTabulador(facturas, tabulador,
            out var comBase, out var comBono);

        // 4) ¿Cumplió? Y si no, ¿qué se paga?
        var r = cuota is null
            ? SinMetas(cobranzaResumen, comBase, comBono, productos, sinTabulador)
            : EvaluarMetas(cuota, cobranzaResumen, comBase, comBono, productos, sinTabulador);

        return (r, nombreVendedor);
    }

    /// <summary>
    /// Evalúa el operador lógico sobre las metas ACTIVAS y decide el monto final.
    /// <para>
    /// Sobre PagoMinimo: se paga <c>max(comisión base, monto mínimo)</c>, no el monto pelón.
    /// Es un piso garantizado — si el vendedor vendió mucho (por encima del mínimo) jamás debe
    /// cobrar menos por no haber llegado a la meta; la lectura literal del documento original
    /// («paga el monto fijo») habría recortado comisiones ganadas.
    /// </para>
    /// </summary>
    private static ResultadoCalculo EvaluarMetas(
        CuotaVendedor cuota, CobranzaResumenDto cob, decimal comBase, decimal comBono,
        List<ComisionProductoDto> productos, List<string> sinTabulador)
    {
        // Meta activa = existe Y es mayor a cero. Una meta en 0 se cumple sola, así que
        // tratarla como activa regalaría el bono a quien no vendió nada.
        var tieneDinero = cuota.MetaDinero is > 0;
        var tieneLitros = cuota.MetaLitros is > 0;

        var pctDinero = tieneDinero ? Redondear(cob.NetoCobradoSinIva / cuota.MetaDinero!.Value * 100m) : 0m;
        var pctLitros = tieneLitros ? Redondear(cob.LitrosCobrados / cuota.MetaLitros!.Value * 100m) : 0m;

        var cumpleDinero = tieneDinero && cob.NetoCobradoSinIva >= cuota.MetaDinero!.Value;
        var cumpleLitros = tieneLitros && cob.LitrosCobrados >= cuota.MetaLitros!.Value;

        // SOLO_DINERO / SOLO_VOLUMEN ignoran la otra meta aunque esté capturada (GuardarCuotaAsync
        // valida que la meta exigida exista), así que cada operador mira sólo lo que le toca.
        var cumplio = cuota.Operador switch
        {
            OperadorLogico.SoloDinero => cumpleDinero,
            OperadorLogico.SoloVolumen => cumpleLitros,
            OperadorLogico.Y => cumpleDinero && cumpleLitros,
            OperadorLogico.O => cumpleDinero || cumpleLitros,
            _ => false
        };

        var detalle = Explicar(cuota, cob, pctDinero, pctLitros, cumpleDinero, cumpleLitros, cumplio);

        decimal final;
        var aplicoMinimo = false;
        if (cumplio)
        {
            final = comBono;
        }
        else if (cuota.AccionIncumplimiento == AccionIncumplimiento.PagoMinimo)
        {
            final = Math.Max(comBase, cuota.MontoPagoMinimo);
            aplicoMinimo = final == cuota.MontoPagoMinimo && cuota.MontoPagoMinimo > comBase;
        }
        else
        {
            final = 0m; // CeroComision
        }

        return new ResultadoCalculo(cob, cuota, productos, sinTabulador,
            pctDinero, pctLitros, cumplio, detalle, comBase, comBono, Redondear(final), aplicoMinimo);
    }

    /// <summary>Explicación legible del porqué cumplió o no (queda congelada en el cierre).</summary>
    private static string Explicar(CuotaVendedor cuota, CobranzaResumenDto cob,
        decimal pctDinero, decimal pctLitros, bool cumpleDinero, bool cumpleLitros, bool cumplio)
    {
        var partes = new List<string>();
        if (cuota.MetaDinero is > 0)
            partes.Add($"dinero {cob.NetoCobradoSinIva:C} de {cuota.MetaDinero:C} " +
                       $"({pctDinero:N1}%) {(cumpleDinero ? "OK" : "FALTA")}");
        if (cuota.MetaLitros is > 0)
            partes.Add($"litros {cob.LitrosCobrados:N2} de {cuota.MetaLitros:N2} " +
                       $"({pctLitros:N1}%) {(cumpleLitros ? "OK" : "FALTA")}");

        var regla = cuota.Operador switch
        {
            OperadorLogico.SoloDinero => "requisito: dinero",
            OperadorLogico.SoloVolumen => "requisito: litros",
            OperadorLogico.Y => "requisito: dinero Y litros",
            OperadorLogico.O => "requisito: dinero O litros",
            _ => "requisito: operador no reconocido"
        };

        return $"{regla}. {string.Join("; ", partes)} => {(cumplio ? "CUMPLE" : "NO CUMPLE")}";
    }

    /// <summary>Sin cuota configurada: se reporta lo cobrado y se paga la base, sin bono posible.</summary>
    private static ResultadoCalculo SinMetas(
        CobranzaResumenDto cob, decimal comBase, decimal comBono,
        List<ComisionProductoDto> productos, List<string> sinTabulador) =>
        new(cob, null, productos, sinTabulador, 0m, 0m, false,
            "Sin cuota configurada: se paga el % base y no hay bono por cumplir.",
            comBase, comBono, comBase, false);

    /// <summary>Centavos al centavo, sin sorpresas de punto flotante.</summary>
    private static decimal Redondear(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

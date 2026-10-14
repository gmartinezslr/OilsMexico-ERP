namespace OilsMexico.Domain.Services;

/// <summary>
/// Convención del cierre de periodo contable (§1.3): concepto único con el que se
/// "marca" el asiento de costo de ventas generado al cerrar un mes. La misma cadena
/// se usa para encontrar el asiento (lectura del Estado de Resultados) y para no
/// duplicarlo si el cierre se reintenta.
/// </summary>
public static class CierresContables
{
    /// <summary>Tipo de asiento que registra el cierre de un periodo.</summary>
    public const string TipoAsiento = "Cierre";

    /// <summary>Concepto exacto del asiento de cierre de (año, mes) de una sucursal.</summary>
    public static string ConceptoCierre(int anio, int mes) =>
        $"Cierre {anio:0000}-{mes:00} · Costo de ventas FIFO";
}
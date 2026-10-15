namespace OilsMexico.Domain.Services;

/// <summary>
/// Regla de periodo contable cerrado (Prioridad 1.1 — decisión de negocio documentada):
/// NINGUNA operación que genere o revierta movimientos contables/inventario se acepta
/// con fecha dentro de un mes cerrado. La única vía es reabrir el periodo (solo Admin
/// vía <c>ICierreContableService.ReabrirAsync</c>); ni siquiera Admin puede escribir
/// directo en un mes cerrado. Aplica a: POS/OV→venta, Notas de Crédito, devoluciones y
/// surtidos del Historial, cobros REP, recepciones/pagos de compra, gastos (su póliza),
/// ajustes/traspasos de almacén y asientos manuales (estos ya se bloqueaban en
/// <c>CuentasContablesService</c>). Cotizaciones y Órdenes (documentos previos, sin
/// póliza ni kardex) NO se bloquean: pueden creerse/editarse libremente; la facturación
/// de la orden sí se bloquea porque genera la venta. Implementación: fail-closed antes
/// de tocar la BD, con mensaje que indica el periodo yyyy-MM.
/// </summary>
public static class ReglaPeriodoCerrado
{
    public static string Mensaje(DateTime fechaLocal) =>
        $"No se pueden registrar operaciones en el mes {fechaLocal:yyyy-MM} porque el periodo contable está cerrado. " +
        "Un administrador debe reabrir el periodo para poder modificarlo.";

    public static DateTime ALocal(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha.ToLocalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Local)
    };
}

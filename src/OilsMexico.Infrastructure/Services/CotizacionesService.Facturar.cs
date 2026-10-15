using OilsMexico.Application.DTOs;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class CotizacionesService
{
    public Task<OrdenVentaFacturarResult> ConvertirOrdenAVentaAsync(
        int ordenId, OrdenVentaFacturarRequest req, CancellationToken ct = default) =>
        facturacionOrden.FacturarOrdenVentaAsync(ordenId, req, ct);
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Enums;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Envíos masivos por correo: comunicados (asunto/cuerpo libres con variables) a clientes
/// o proveedores, y recordatorios de saldo con la plantilla de cobro. Un destinatario sin
/// correo o fallido no detiene el lote: se contabiliza en <see cref="ResultadoEnvioMasivoDto"/>.
/// </summary>
public sealed partial class EmailService
{
    private const int MaxDestinatariosMasivo = 500;

    public async Task<ResultadoEnvioMasivoDto> EnviarComunicadoAsync(ComunicadoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Asunto))
            throw new InvalidOperationException("El asunto del comunicado es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Cuerpo))
            throw new InvalidOperationException("El cuerpo del comunicado es obligatorio.");
        var empresa = await EmpresaAsync(ct);

        List<(int Id, string Nombre, string? Email)> destinos;
        if (request.TipoDestinatario == ComunicadoRequest.Clientes)
        {
            var filas = await db.Clientes.AsNoTracking()
                .Where(c => request.SoloActivos ? c.Activo : true)
                .OrderBy(c => c.Nombre).Take(MaxDestinatariosMasivo)
                .Select(c => new { c.Id, c.Nombre, c.Email })
                .ToListAsync(ct);
            destinos = filas.Select(x => (x.Id, x.Nombre, x.Email)).ToList();
        }
        else if (request.TipoDestinatario == ComunicadoRequest.Proveedores)
        {
            var filas = await db.Proveedores.AsNoTracking()
                .Where(p => request.SoloActivos ? p.Activo : true)
                .OrderBy(p => p.Nombre).Take(MaxDestinatariosMasivo)
                .Select(p => new { p.Id, p.Nombre, p.Email })
                .ToListAsync(ct);
            destinos = filas.Select(x => (x.Id, x.Nombre, x.Email)).ToList();
        }
        else
        {
            throw new InvalidOperationException("Tipo de destinatario inválido: usa 'clientes' o 'proveedores'.");
        }

        // Filtro opcional "solo con saldo": clientes con facturas abiertas / proveedores con CxP.
        if (request.SoloConSaldo)
        {
            if (request.TipoDestinatario == ComunicadoRequest.Clientes)
            {
                var conSaldo = await db.Facturas.AsNoTracking()
                    .Where(f => f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion)
                    .Select(f => f.ClienteId).Distinct().ToListAsync(ct);
                destinos = destinos.Where(d => conSaldo.Contains(d.Id)).ToList();
            }
            else
            {
                var conSaldo = await db.Compras.AsNoTracking()
                    .Where(c => c.Estado != EstadoCompra.Cancelada && c.Total - c.MontoPagado > 0.01m)
                    .Select(c => c.ProveedorId).Distinct().ToListAsync(ct);
                destinos = destinos.Where(d => conSaldo.Contains(d.Id)).ToList();
            }
        }

        return await EnviarLoteAsync(destinos, request.Asunto, request.Cuerpo, empresa, ct);
    }

    public async Task<ResultadoEnvioMasivoDto> EnviarRecordatoriosSaldoAsync(string? asunto = null, string? cuerpo = null, CancellationToken ct = default)
    {
        // Mismo criterio cash-basis que EstadoCuentasService (vía el servicio, sin duplicar la lógica).
        var estados = await estadoCuentas.ListarEstadosCuentasAsync(ctx.SucursalId, ct);
        var conSaldo = estados.Where(e => e.SaldoPendiente > 0.01m).ToList();
        var destinos = conSaldo.Select(e => (e.ClienteId, e.Nombre, string.IsNullOrWhiteSpace(e.Email) ? null : e.Email)).ToList();
        if (destinos.Count == 0)
            return new ResultadoEnvioMasivoDto(0, 0, 0, ["No hay clientes con saldo pendiente en esta sucursal."]);

        // Por defecto se usa la plantilla de cobro por correo (la misma de PlantillasMensaje).
        var plantilla = plantillas.ListarPlantillas().FirstOrDefault(p => p.Clave == "cobro-correo");
        var asuntoFinal = string.IsNullOrWhiteSpace(asunto) ? "Recordatorio de pago — factura pendiente" : asunto.Trim();
        var cuerpoFinal = string.IsNullOrWhiteSpace(cuerpo)
            ? plantilla?.Cuerpo ?? "Estimado {Nombre}: su factura {Folio} por ${Total} del {Fecha} sigue pendiente de pago. — {Empresa}"
            : cuerpo.Trim();

        // Variables por destinatario: el saldo sale del estado de cuenta individual.
        var detallePorCliente = conSaldo.ToDictionary(e => e.ClienteId);
        var empresa = await EmpresaAsync(ct);
        int enviados = 0, fallidos = 0, sinCorreo = 0;
        var errores = new List<string>();
        foreach (var (id, nombre, email) in destinos)
        {
            if (!MailboxAddress.TryParse(email, out _)) { sinCorreo++; continue; }
            try
            {
                var e = detallePorCliente[id];
                var texto = plantillas.RellenarPlantilla(cuerpoFinal, nombre, null, e.SaldoPendiente, DateTime.Today, empresa);
                var cuerpoHtml = PlantillaHtml("Recordatorio de saldo",
                    "<p>" + WebUtility.HtmlEncode(texto).Replace("\n", "<br/>") + "</p>" +
                    "<p style=\"text-align:right;\">Saldo pendiente: <strong style=\"color:#dc3545;\">"
                    + e.SaldoPendiente.ToString("C2") + "</strong></p>");
                await EnviarAsync(new EmailMensajeDto(email!, asuntoFinal, cuerpoHtml, nombre), ct);
                enviados++;
            }
            catch (Exception ex)
            {
                fallidos++;
                if (errores.Count < 10) errores.Add($"{nombre}: {ex.Message}");
            }
        }
        return new ResultadoEnvioMasivoDto(enviados, fallidos, sinCorreo, errores);
    }

    private async Task<ResultadoEnvioMasivoDto> EnviarLoteAsync(
        List<(int Id, string Nombre, string? Email)> destinos, string asunto, string cuerpo, string empresa, CancellationToken ct)
    {
        int enviados = 0, fallidos = 0, sinCorreo = 0;
        var errores = new List<string>();
        foreach (var (_, nombre, email) in destinos)
        {
            if (!MailboxAddress.TryParse(email, out _)) { sinCorreo++; continue; }
            try
            {
                // Variables por destinatario ({Nombre}, {Fecha}, {Empresa}…; Folio/Total no aplican).
                var texto = plantillas.RellenarPlantilla(cuerpo, nombre, null, null, DateTime.Today, empresa);
                var cuerpoHtml = PlantillaHtml(asunto, "<p>" + WebUtility.HtmlEncode(texto).Replace("\n", "<br/>") + "</p>");
                await EnviarAsync(new EmailMensajeDto(email!, asunto, cuerpoHtml, nombre), ct);
                enviados++;
            }
            catch (Exception ex)
            {
                fallidos++;
                if (errores.Count < 10) errores.Add($"{nombre}: {ex.Message}");
            }
        }
        return new ResultadoEnvioMasivoDto(enviados, fallidos, sinCorreo, errores);
    }
}

using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Búsqueda global (Ctrl+K) + plantillas de mensajes operativos.
/// Una sola interfaz para no dispersar la ayuda/productividad en N servicios (SRP):
/// buscar por texto en clientes, productos, proveedores, ventas y compras, y
/// resolver plantillas con variables {Nombre}, {Folio}, {Total}, {Fecha}.
/// Respeta aislamiento multi-sucursal: las ventas/compras se filtran por sucursal.
/// </summary>
public interface IBusquedaGlobalService
{
    Task<List<BusquedaGlobalDto>> BuscarAsync(string texto, int sucursalId, CancellationToken ct = default);

    /// <summary>Catálogo de plantillas (WhatsApp/correo/impresión) con variables.</summary>
    List<PlantillaMensajeDto> ListarPlantillas();

    /// <summary>Rellena {Nombre}, {Folio}, {Total}, {Fecha}, {Empresa} en el cuerpo.</summary>
    string RellenarPlantilla(string cuerpo, string? nombre, string? folio, decimal? total, DateTime? fecha, string? empresa);
}

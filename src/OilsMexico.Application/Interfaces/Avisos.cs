using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Avisos internos del ERP (campana del layout). Implementación en memoria por
/// circuito: cada usuario ve los suyos sin tabla nueva ni SignalR extra.
/// Tipos: info | exito | aviso | error.
/// </summary>
public interface IAvisos
{
    event Action? Cambio;
    IReadOnlyList<AvisoDto> Lista { get; }
    int NoLeidos { get; }
    void Agregar(string titulo, string? detalle = null, string tipo = "info");
    void MarcarLeidos();
    void Limpiar();
}

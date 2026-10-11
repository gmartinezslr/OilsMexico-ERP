using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Web.Services;

/// <summary>
/// Buzón de avisos por circuito (scoped = por usuario). Sin persistencia:
/// informa lo que pasa en la sesión (venta cobrada, código no encontrado,
/// cliente creado, corte abierto). Capado a 30 para no crecer sin cota.
/// </summary>
public sealed class AvisosService : IAvisos
{
    private readonly List<AvisoDto> _avisos = [];
    private int _seq;

    public event Action? Cambio;

    public IReadOnlyList<AvisoDto> Lista => _avisos.OrderByDescending(a => a.FechaUtc).ToList();

    public int NoLeidos => _avisos.Count(a => !a.Leido);

    public void Agregar(string titulo, string? detalle = null, string tipo = "info")
    {
        _avisos.Add(new AvisoDto(++_seq, titulo, detalle, tipo, DateTime.UtcNow, false));
        while (_avisos.Count > 30) _avisos.RemoveAt(0);
        Cambio?.Invoke();
    }

    public void MarcarLeidos()
    {
        for (var i = 0; i < _avisos.Count; i++)
            _avisos[i] = _avisos[i] with { Leido = true };
        Cambio?.Invoke();
    }

    public void Limpiar()
    {
        _avisos.Clear();
        Cambio?.Invoke();
    }
}

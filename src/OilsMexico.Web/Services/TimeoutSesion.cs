using OilsMexico.Application.Interfaces;

namespace OilsMexico.Web.Services;

/// <summary>
/// Temporizador de inactividad por circuito (Scoped). Un <see cref="PeriodicTimer"/> compara cada pocos
/// segundos el tiempo transcurrido desde la última actividad del usuario; al superar el plazo emite
/// <see cref="Expirado"/>. Robusto ante actividades frecuentes (no cancela tareas en curso) y tolerante a
/// la disposición concurrente del circuito (componente y servicio comparten el ciclo de vida).
/// </summary>
public sealed class TimeoutSesion : ITimeoutSesion, IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();
    private long _ultimaActividadTicks = DateTime.UtcNow.Ticks;
    private bool _disposed;

    public event Func<Task>? Expirado;

    public void Iniciar(TimeSpan inactividad)
    {
        _ = Task.Run(() => VigilarAsync(inactividad));
    }

    private async Task VigilarAsync(TimeSpan inactividad)
    {
        // Revisión periódica corta: suficiente para reaccionar a la caducidad con poco margen.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Min(5, Math.Max(1, inactividad.TotalSeconds))));
        try
        {
            while (await timer.WaitForNextTickAsync(TokenActual()))
            {
                var inactivo = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _ultimaActividadTicks));
                if (inactivo < inactividad) continue;
                if (Expirado is { } handler) await handler.Invoke();
                return; // La caducidad detiene el temporizador; el circuito navega a /login.
            }
        }
        catch (OperationCanceledException)
        {
            // Detenido (cierre de sesión o desmontaje del circuito).
        }
        catch (ObjectDisposedException)
        {
            // El circuito se dispuso mientras el vigilante corría.
        }
    }

    public void RegistrarActividad()
    {
        lock (_gate) { if (_disposed) return; }
        Interlocked.Exchange(ref _ultimaActividadTicks, DateTime.UtcNow.Ticks);
    }

    public void Detener()
    {
        lock (_gate) { if (_disposed) return; _cts.Cancel(); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }
    }

    private CancellationToken TokenActual()
    {
        lock (_gate) { return _cts.Token; }
    }
}



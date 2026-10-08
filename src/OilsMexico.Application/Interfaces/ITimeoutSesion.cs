namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Controla la caducidad por inactividad de la sesión. El circuito notifica la actividad del usuario
/// (vía <see cref="RegistrarActividad"/>); si transcurre el plazo configurado sin actividad, se emite
/// <see cref="Expirado"/> para cerrar la sesión y volver al login.
/// </summary>
public interface ITimeoutSesion
{
    /// <summary>Se dispara cuando vence el plazo de inactividad. Debe cerrar sesión y redirigir a /login.</summary>
    event Func<Task>? Expirado;

    /// <summary>Arranca (o reprograma) el temporizador con el plazo indicado.</summary>
    void Iniciar(TimeSpan inactividad);

    /// <summary>Reinicia el conteo de inactividad por actividad del usuario.</summary>
    void RegistrarActividad();

    /// <summary>Detiene el temporizador (p. ej. al cerrar sesión o desmontar el circuito).</summary>
    void Detener();
}

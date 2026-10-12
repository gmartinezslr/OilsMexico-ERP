namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Reloj de la aplicación. Fuente única de "ahora": las páginas y servicios ya no
/// llaman a <c>DateTime.UtcNow</c> / <c>DateTime.Now</c> dispersos, de modo que
/// las reglas (vigencia de contraseña, bloqueos, vencimientos) puedan probarse
/// con fechas controladas.
/// </summary>
public interface IReloj
{
    /// <summary>Instante actual en UTC.</summary>
    DateTime AhoraUtc { get; }
}

/// <summary>Reloj basado en el reloj del sistema (implementación de producción).</summary>
public sealed class RelojSistema : IReloj
{
    public DateTime AhoraUtc => DateTime.UtcNow;
}

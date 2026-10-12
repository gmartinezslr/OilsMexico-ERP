using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>
/// Reglas de complejidad de contraseña (ISP): <see cref="IAuthService"/> depende de esta
/// interfaz mínima en lugar de arrastrar toda la fachada a las páginas de usuarios,
/// que antes duplicaban la política campo por campo.
/// </summary>
public interface IPoliticaPassword
{
    /// <summary>Política vigente (longitud mínima, historial, vigencia, complejidad).</summary>
    Task<PasswordConfigDto> ObtenerAsync(CancellationToken ct = default);

    /// <summary>
    /// Valida una contraseña candidata contra la política. Devuelve la lista de fallos
    /// (vacía si cumple) para poder mostrarlos todos juntos en el formulario.
    /// </summary>
    Task<(bool Valida, List<string> Fallos)> ValidarAsync(string password, CancellationToken ct = default);

    /// <summary>Hash de contraseña con el algoritmo del sistema (Identity v3).</summary>
    string Hash(string password);
}

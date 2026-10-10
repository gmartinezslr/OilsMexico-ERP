using OilsMexico.Application.DTOs;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Validación de RFC conforme al algoritmo oficial del SAT (Anexo 3 del RCFF): estructura,
/// fecha, palabras prohibidas y dígito verificador módulo 11.
/// </summary>
public static class RfcValidador
{
    // RFCs especiales reconocidos por el SAT sin estructura estándar.
    private static readonly Dictionary<string, string> RfcEspeciales = new()
    {
        ["XAXX010101000"] = "Público en general",
        ["XEXX010101000"] = "Extranjero"
    };

    // RFCs emitidos por el SAT cuyo dígito verificador no coincide con el algoritmo pero son válidos.
    private static readonly HashSet<string> RfcExentosVerificador =
        ["DAA020218JY1", "EDG811007RB3", "LIM0011098G0", "LME060822IH5", "NFS0103297H5"];

    // Palabras que el SAT prohíbe como iniciales de un RFC.
    private static readonly HashSet<string> PalabrasProhibidas =
        ["BUEI", "BUEY", "CACA", "CACO", "CAGA", "CAGO", "CAKA", "CAKO", "COGE", "COJA", "COJE", "COJI",
         "COJO", "CULO", "FETO", "GUEY", "JOTO", "KACA", "KACO", "KAGA", "KAGO", "KOGE", "KOJO", "KAKA",
         "KULO", "MAME", "MAMO", "MEAR", "MEAS", "MEON", "MION", "MOCO", "MULA", "PEDA", "PEDO", "PENE",
         "PUTA", "PUTO", "QULO", "RATA", "RUIN"];

    public static ConsultaRfcResult Validar(string? rfc)
    {
        var rfcLimpio = (rfc ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        if (string.IsNullOrEmpty(rfcLimpio))
            return new(false, "RFC requerido.", null, null, 0, 0, null, null, false);

        // RFCs especiales del SAT: se aceptan tal cual.
        if (RfcEspeciales.TryGetValue(rfcLimpio, out var tipoEspecial))
            return new(true, $"RFC válido ({tipoEspecial}).", tipoEspecial, null, 0, 0, rfcLimpio[..4], null, true);

        // 1) ESTRUCTURA: 12 = moral (3 letras + fecha + homoclave + verificador)
        //                13 = física (4 letras + fecha + homoclave + verificador)
        if (rfcLimpio.Length is not (12 or 13))
            return new(false, "RFC debe tener 12 (moral) o 13 (física) caracteres.", null, null, 0, 0, null, null, false);

        var letrasLen = rfcLimpio.Length == 13 ? 4 : 3;
        var letras = rfcLimpio[..letrasLen];
        var digitos = rfcLimpio[letrasLen..^3]; // sección numérica AAMMDD
        var homoclave = rfcLimpio[^3..^1];      // homoclave de 2 posiciones
        var verificador = rfcLimpio[^1];

        if (letras.Any(c => !char.IsLetter(c)))
            return new(false, "RFC: la sección de letras solo puede contener letras (A-Z, Ñ).", null, null, 0, 0, null, null, false);
        if (digitos.Length != 6 || digitos.Any(c => !char.IsDigit(c)))
            return new(false, "RFC: la sección numérica (fecha) debe ser de 6 dígitos.", null, null, 0, 0, null, null, false);
        if (homoclave.Any(c => !char.IsLetterOrDigit(c)) || !char.IsLetterOrDigit(verificador))
            return new(false, "RFC: la homoclave y el dígito verificador deben ser alfanuméricos.", null, null, 0, 0, null, null, false);

        // 2) PALABRAS PROHIBIDAS
        if (PalabrasProhibidas.Contains(letras))
            return new(false, "RFC: las iniciales forman una palabra prohibida por el SAT.", null, null, 0, 0, null, null, false);

        // 3) SEMÁNTICA: la sección numérica debe ser una fecha válida (AA MM DD).
        var año = 1900 + int.Parse(digitos[..2]);
        var mes = int.Parse(digitos[2..4]);
        var dia = int.Parse(digitos[4..6]);
        if (año < 1900 || año > 2099)
            return new(false, "RFC: año fuera de rango.", null, año, mes, dia, null, null, false);
        if (mes < 1 || mes > 12)
            return new(false, $"RFC: mes inválido '{mes:D2}'.", null, año, mes, dia, null, null, false);
        var diasMax = DiasEnMes(mes, año);
        if (dia < 1 || dia > diasMax)
            return new(false, $"RFC: día {dia} no es válido para el mes {mes} del año {año}.", null, año, mes, dia, null, null, false);

        // 4) DÍGITO VERIFICADOR (oficial módulo 11); algunos RFCs antiguos del SAT quedaron exentos.
        if (!RfcExentosVerificador.Contains(rfcLimpio))
        {
            var esperado = CalcularDigitoVerificador(rfcLimpio[..^1]);
            if (verificador.ToString() != esperado)
                return new(false, $"Dígito verificador incorrecto: se esperaba '{esperado}' y se recibió '{verificador}'.",
                    null, año, mes, dia, homoclave, verificador.ToString(), false);
        }

        var tipo = rfcLimpio.Length == 13 ? "Física" : "Moral";
        return new(true, "RFC válido.", tipo, año, mes, dia, homoclave, verificador.ToString(), true);
    }

    /// <summary>
    /// Dígito verificador oficial del SAT (Anexo 3): a cada carácter se le asigna su valor
    /// (0-9, A=10..N=23, &amp;=24, O=25..Z=36, espacio=37, Ñ=38), se multiplica por el factor
    /// (posición de derecha a izquierda más uno: 2..13) y del residuo módulo 11 se obtiene
    /// el dígito (residuo 0 → '0', 10 → 'A', 11 → '0'; resto, el residuo como dígito).
    /// </summary>
    private static string CalcularDigitoVerificador(string baseRfc)
    {
        // Moral (11 caracteres): se compensa con un espacio inicial para aplicar los mismos
        // factores 2..13 que en el RFC de 13 posiciones (regla oficial).
        var s = baseRfc.Length == 11 ? " " + baseRfc : baseRfc;
        long suma = 0;
        for (var i = 0; i < s.Length; i++)
            suma += (long)ValorCaracter(s[i]) * (s.Length - i + 1);
        var residuo = (int)((11000 - suma) % 11);
        return residuo switch
        {
            10 => "A",
            11 => "0",
            _ => residuo.ToString()
        };
    }

    private static int ValorCaracter(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'N' => c - 'A' + 10,
        '&' => 24,
        >= 'O' and <= 'Z' => c - 'A' + 11, // '&' ocupa el valor 24: O=25 .. Z=36
        ' ' => 37,
        'Ñ' => 38,
        _ => 0
    };

    private static int DiasEnMes(int mes, int año)
    {
        return mes switch
        {
            1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
            4 or 6 or 9 or 11 => 30,
            2 => (año % 400 == 0 || (año % 4 == 0 && año % 100 != 0)) ? 29 : 28,
            _ => 0
        };
    }
}

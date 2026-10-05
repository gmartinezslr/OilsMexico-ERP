using Microsoft.AspNetCore.Components;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;

namespace OilsMexico.Web.Components.Shared;

public partial class DireccionSepomex : ComponentBase
{
    private DireccionSepomexDto? direccion;
    private List<AsentamientoDto> colonias = [];
    private bool buscandoCp, cpNoEncontrado;
    private CancellationTokenSource? _cts;
    // Último CP por el que ya se disparó la búsqueda; evita duplicarla desde OnParametersSetAsync.
    private string? _ultimoCpBuscado;

    [Parameter] public string? CodigoPostal { get; set; }
    [Parameter] public EventCallback<string?> CodigoPostalChanged { get; set; }
    [Parameter] public string? Colonia { get; set; }
    [Parameter] public EventCallback<string?> ColoniaChanged { get; set; }
    [Parameter] public string? Calle { get; set; }
    [Parameter] public EventCallback<string?> CalleChanged { get; set; }
    [Parameter] public string? NumeroExterior { get; set; }
    [Parameter] public EventCallback<string?> NumeroExteriorChanged { get; set; }
    [Parameter] public string? NumeroInterior { get; set; }
    [Parameter] public EventCallback<string?> NumeroInteriorChanged { get; set; }
    [Parameter] public string? Municipio { get; set; }
    [Parameter] public EventCallback<string?> MunicipioChanged { get; set; }
    [Parameter] public string? Estado { get; set; }
    [Parameter] public EventCallback<string?> EstadoChanged { get; set; }
    [Parameter] public string? Ciudad { get; set; }
    [Parameter] public EventCallback<string?> CiudadChanged { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        // El padre puede cargar un registro (Nuevo/Editar) sin que el usuario teclee el CP;
        // en ese caso hay que poblar el combo de colonias igual.
        var cp = (CodigoPostal ?? "").Trim();
        if (cp.Length == 5 && cp.All(char.IsDigit))
        {
            if (!string.Equals(_ultimoCpBuscado, cp, StringComparison.Ordinal))
                await BuscarPorCpAsync(cp, debounce: false);
        }
        else if (_ultimoCpBuscado is not null || colonias.Count > 0 || direccion is not null || buscandoCp)
        {
            _ultimoCpBuscado = null;
            _cts?.Cancel();
            buscandoCp = false;
            direccion = null; colonias = []; cpNoEncontrado = false;
        }

        if (!buscandoCp && colonias.Count > 0)
        {
            // Al cargar un registro (o al cambiar de uno con el mismo CP) hay que
            // reconciliar la colonia guardada con el catálogo y, si el CP tiene una
            // sola colonia, dejarla preseleccionada. Es idempotente: no dispara
            // notificaciones cuando todo ya coincide.
            await ReconciliarColoniaAsync();
            await SeleccionarUnicaColoniaAsync();
        }

        await base.OnParametersSetAsync();
    }

    private async Task AlEscribirCp(ChangeEventArgs e)
    {
        CodigoPostal = e.Value?.ToString()?.Trim() ?? "";
        var cp = CodigoPostal;
        var valido = cp.Length == 5 && cp.All(char.IsDigit);
        // Se marca/limpia ANTES de notificar al padre: el re-render del padre invoca a
        // OnParametersSetAsync y éste no debe disparar una búsqueda duplicada del mismo CP.
        _ultimoCpBuscado = valido ? cp : null;
        if (!valido)
        {
            _cts?.Cancel();
            buscandoCp = false;
            direccion = null; colonias = []; cpNoEncontrado = false;
        }
        await CodigoPostalChanged.InvokeAsync(CodigoPostal);
        if (valido)
            await BuscarPorCpAsync(cp, debounce: true);
    }

    private async Task BuscarPorCpAsync(string cp, bool debounce)
    {
        _ultimoCpBuscado = cp;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        buscandoCp = true; cpNoEncontrado = false;
        StateHasChanged();
        try
        {
            if (debounce)
                await Task.Delay(350, token);

            var resultado = await ConsultarSepomexAsync(cp, token);
            token.ThrowIfCancellationRequested();
            direccion = resultado;
            if (resultado is null)
            {
                // CP fuera del catálogo: no hay opciones que ofrecer; se conserva la
                // colonia guardada como única opción visible.
                cpNoEncontrado = true;
                colonias = [];
            }
            else
            {
                colonias = resultado.Asentamientos;
                await Setear("Municipio", resultado.Municipio);
                await Setear("Estado", resultado.Estado);
                await Setear("Ciudad", resultado.Ciudad);
                await ReconciliarColoniaAsync();
                // Si el CP tiene una sola colonia, dejarla ya seleccionada
                // (el combo no mostrará placeholder y no podrá cambiarse).
                await SeleccionarUnicaColoniaAsync();
            }
        }
        catch (OperationCanceledException) { return; }
        finally
        {
            // Si otra búsqueda ya canceló este token, es ella quien apaga el indicador.
            if (!token.IsCancellationRequested)
            {
                buscandoCp = false;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// Consulta el catálogo en un scope propio: el DbContext es scoped y el render
    /// inicial puede tener otras consultas en vuelo sobre la misma instancia
    /// (EF lanza "A second operation was started on this context instance").
    /// </summary>
    private async Task<DireccionSepomexDto?> ConsultarSepomexAsync(string cp, CancellationToken token)
    {
        using var scope = ScopeFactory.CreateScope();
        var sepomex = scope.ServiceProvider.GetRequiredService<ISepomexService>();
        return await sepomex.BuscarPorCpAsync(cp, token);
    }

    private async Task ReconciliarColoniaAsync()
    {
        if (string.IsNullOrWhiteSpace(Colonia) || colonias.Count == 0)
            return;

        var match = colonias.FirstOrDefault(c => EsMismaColonia(c.Asentamiento, Colonia));
        if (match is null)
        {
            // La colonia guardada no existe en el catálogo de este CP: se limpia para
            // forzar la reselección desde el combo (evita valores arbitrarios).
            await Setear("Colonia", "");
        }
        else if (!string.Equals(match.Asentamiento, Colonia, StringComparison.Ordinal))
        {
            // Normaliza al nombre oficial del catálogo.
            await Setear("Colonia", match.Asentamiento);
        }
    }

    /// <summary>
    /// Si el CP tiene una sola colonia en el catálogo, la preselecciona (y sincroniza
    /// CP/municipio/estado/ciudad con ella). El combo no emite el placeholder en este
    /// caso, así que la única opción posible es esa colonia: no puede cambiarse.
    /// </summary>
    private async Task SeleccionarUnicaColoniaAsync()
    {
        if (colonias.Count != 1)
            return;

        var unica = colonias[0];
        if (!string.Equals((Colonia ?? "").Trim(), unica.Asentamiento, StringComparison.Ordinal))
            await Setear("Colonia", unica.Asentamiento);
        if (!string.Equals((CodigoPostal ?? "").Trim(), unica.CodigoPostal, StringComparison.Ordinal))
            await Setear("CodigoPostal", unica.CodigoPostal);
        if (!string.Equals(Municipio ?? "", unica.Municipio ?? "", StringComparison.Ordinal))
            await Setear("Municipio", unica.Municipio);
        if (!string.Equals(Estado ?? "", unica.Estado ?? "", StringComparison.Ordinal))
            await Setear("Estado", unica.Estado);
        if (!string.Equals(Ciudad ?? "", unica.Ciudad ?? "", StringComparison.Ordinal))
            await Setear("Ciudad", unica.Ciudad);
    }

    private async Task AlElegirColonia(ChangeEventArgs e)
    {
        Colonia = e.Value?.ToString() ?? "";
        await ColoniaChanged.InvokeAsync(Colonia);
        var match = colonias.FirstOrDefault(c => EsMismaColonia(c.Asentamiento, Colonia));
        if (match is not null)
        {
            if (string.IsNullOrWhiteSpace(CodigoPostal) || CodigoPostal != match.CodigoPostal)
                await Setear("CodigoPostal", match.CodigoPostal);
            await Setear("Municipio", match.Municipio);
            await Setear("Estado", match.Estado);
            await Setear("Ciudad", match.Ciudad);
        }
    }

    private async Task Setear(string prop, string? valor)
    {
        switch (prop)
        {
            case "CodigoPostal": CodigoPostal = valor; await CodigoPostalChanged.InvokeAsync(valor); break;
            case "Colonia": Colonia = valor; await ColoniaChanged.InvokeAsync(valor); break;
            case "Municipio": Municipio = valor; await MunicipioChanged.InvokeAsync(valor); break;
            case "Estado": Estado = valor; await EstadoChanged.InvokeAsync(valor); break;
            case "Ciudad": Ciudad = valor; await CiudadChanged.InvokeAsync(valor); break;
        }
    }

    private static bool EsMismaColonia(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private bool EsColoniaActual(string? valor) => EsMismaColonia(valor, Colonia);

    private bool HayOpcionesColonia => colonias.Count > 0 || !string.IsNullOrWhiteSpace(Colonia);

    private string PlaceholderColonia =>
        buscandoCp ? "Buscando colonias…"
        : cpNoEncontrado ? "CP no encontrado en catálogo"
        : colonias.Count > 0 ? "Selecciona la colonia"
        : (CodigoPostal ?? "").Trim().Length == 5 ? "Sin colonias para este CP"
        : "Captura un CP de 5 dígitos";
}

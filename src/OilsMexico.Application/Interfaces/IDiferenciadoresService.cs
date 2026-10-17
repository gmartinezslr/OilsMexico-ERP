using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

public interface IDiferenciadoresService
{
    Task<List<ListaPrecioDto>> ListarListasPrecioAsync(CancellationToken ct = default);
    Task<int> GuardarListaPrecioAsync(int id, string nombre, string tipoCliente, bool activa, CancellationToken ct = default);
    Task<List<PrecioVolumenDto>> ListarPreciosVolumenAsync(int listaId, CancellationToken ct = default);
    Task GuardarPrecioVolumenAsync(int id, int listaId, int productoId, decimal cantidadMinima, decimal precio, CancellationToken ct = default);
    Task<PrecioResueltoDto> ResolverPrecioAsync(int productoId, int clienteId, decimal cantidad,
        decimal factorConversion = 1m, CancellationToken ct = default);
    Task<Dictionary<int, PrecioResueltoDto>> ResolverPreciosAsync(IReadOnlyCollection<int> productoIds, int clienteId,
        IReadOnlyDictionary<int, decimal>? factoresConversion = null, CancellationToken ct = default);
    Task<List<CompatibilidadVehiculoDto>> BuscarCompatibilidadAsync(string marca, string modelo, int anio, CancellationToken ct = default);
    Task<List<CompatibilidadVehiculoDto>> ListarCompatibilidadesAsync(CancellationToken ct = default);
    Task GuardarCompatibilidadAsync(int id, CompatibilidadVehiculoRequest request, CancellationToken ct = default);
    Task<List<EvaluacionProveedorDto>> ListarEvaluacionesProveedorAsync(CancellationToken ct = default);
    Task EvaluarProveedorAsync(EvaluacionProveedorRequest request, CancellationToken ct = default);
    Task<List<EntregaRepartoDto>> ListarEntregasAsync(CancellationToken ct = default);
    Task<List<(int Id, string Folio, string Cliente)>> OrdenesParaEntregaAsync(CancellationToken ct = default);
    Task<int> ProgramarEntregaAsync(EntregaCrearRequest request, CancellationToken ct = default);
    Task LiquidarEntregaAsync(EntregaLiquidarRequest request, CancellationToken ct = default);
    Task<List<ControlTamborDto>> ListarControlesTamborAsync(CancellationToken ct = default);
    Task GuardarControlTamborAsync(int id, int productoId, decimal litrosPorTambor, int cerrados, decimal abiertos, CancellationToken ct = default);
    Task<List<EnvaseSaldoDto>> ListarSaldosEnvasesAsync(CancellationToken ct = default);
    Task RegistrarMovimientoEnvaseAsync(EnvaseMovimientoRequest request, CancellationToken ct = default);
    Task<CartaPorteDto> GuardarCartaPorteAsync(CartaPorteRequest request, CancellationToken ct = default);
}

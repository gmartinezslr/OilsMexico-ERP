using OilsMexico.Application.DTOs;

namespace OilsMexico.Application.Interfaces;

/// <summary>Proveedor de sucursal en sesión. REGLA INMUTABLE #1: todo se filtra por aquí.</summary>
public interface ISucursalContext
{
    int SucursalId { get; }
    int UsuarioId { get; }
    string Rol { get; }
    void Establecer(int sucursalId, int usuarioId, string rol);
}

public interface IVentasService
{
    Task<List<ProductoDto>> BuscarProductosAsync(int sucursalId, string? filtro, CancellationToken ct = default);
    Task<VentaPosResult> RegistrarVentaAsync(VentaPosRequest request, CancellationToken ct = default);
    Task<VentaPosResult> SurtirAsync(int facturaId, CancellationToken ct = default);
    Task<VentaPosResult> DevolverAsync(int facturaId, string motivo, CancellationToken ct = default);
}

public interface IInventarioService
{
    Task EntradaCompraAsync(int sucursalId, int productoId, int? loteId, decimal litros, int usuarioId, string? motivo = null, CancellationToken ct = default);
    Task AjusteAsync(int sucursalId, int productoId, int? loteId, decimal stockRealLitros, int usuarioId, string motivo, CancellationToken ct = default);
    Task TraspasoAsync(int origenId, int destinoId, int productoId, decimal litros, int usuarioId, CancellationToken ct = default);
}

/// <summary>REGLA INMUTABLE #2: sellado nativo con System.Security.Cryptography (CSD .key/.cer, SHA-256).</summary>
public interface ICfdiSelladoService
{
    Task<(string CadenaOriginal, string Sello, string XmlSellado)> SellarAsync(int facturaId, CancellationToken ct = default);
}

public interface IPacTimbradoService
{
    /// <summary>Timbra vía Web Service del PAC. En dev/staging puede operar en modo simulado.</summary>
    Task<Guid> TimbrarAsync(string xmlSellado, CancellationToken ct = default);
}

public interface ICatalogosSatService
{
    CatalogosSatDto Obtener();
}

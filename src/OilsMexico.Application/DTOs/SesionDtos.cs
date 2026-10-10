namespace OilsMexico.Application.DTOs;

public sealed record SesionDto(
    int UsuarioId, string Nombre, string Rol, int SucursalId, string SucursalNombre, string? SesionToken = null)
{
    /// <summary>Área funcional legible del rol (para mostrar en UI).</summary>
    public string Area => Rol switch
    {
        "Vendedor" => "Ventas",
        "Almacen" => "Almacén",
        "Conta" => "Contabilidad",
        "Admin" => "Administración",
        _ => Rol
    };

    /// <summary>Iniciales para el avatar (máx. 2 letras).</summary>
    public string Iniciales
    {
        get
        {
            var partes = Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (partes.Length == 0) return "?";
            return partes.Length == 1
                ? partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpperInvariant()
                : $"{partes[0][0]}{partes[^1][0]}".ToUpperInvariant();
        }
    }
}

public sealed record LoginPinRequest(string Pin);
public sealed record LoginResultado(SesionDto? Sesion, string? MensajeError = null, DateTime? BloqueadoHastaUtc = null,
    bool RequiereDosFa = false, bool PasswordExpirada = false, int? UsuarioId = null);

public sealed record AlmacenMovimientoDto(
    int ProductoId, int? LoteId, decimal Litros, string Motivo);

public sealed record KardexDto(
    DateTime Fecha, string Tipo, decimal Litros, string? Motivo,
    string Usuario, int? ReferenciaId);

public sealed record TicketVentaDto(
    string Folio, DateTime Fecha, string Sucursal, string Cajero,
    string Cliente, string FormaPago, List<TicketLineaDto> Lineas,
    decimal Subtotal, decimal Iva, decimal Total, Guid? Uuid);

public sealed record TicketLineaDto(
    string Descripcion, decimal Cantidad, string Unidad,
    decimal Precio, decimal Importe);

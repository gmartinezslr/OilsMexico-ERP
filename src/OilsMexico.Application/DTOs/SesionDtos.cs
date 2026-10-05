namespace OilsMexico.Application.DTOs;

public sealed record SesionDto(
    int UsuarioId, string Nombre, string Rol, int SucursalId, string SucursalNombre);

public sealed record LoginPinRequest(string Pin);

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

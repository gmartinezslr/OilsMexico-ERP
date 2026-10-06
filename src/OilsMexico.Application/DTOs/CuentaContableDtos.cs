namespace OilsMexico.Application.DTOs;

// ---------- Catálogo de cuentas contables (MVP básico) ----------

/// <summary>Cuenta contable del catálogo.</summary>
public sealed record CuentaContableDto(
    int Id, string Codigo, string Nombre, int Tipo, string? Descripcion, decimal SaldoActual);

/// <summary>Tipo de movimiento de la póliza.</summary>
public static class MovimientoTipo
{
    public const int Debito = 1;
    public const int Credito = 2;
}

/// <summary>Débito o crédito de un detalle de asiento.</summary>
public sealed record DetalleAsientoDto(
    int Id, int CuentaId, string CuentaNombre, string CuentaCodigo, int TipoMovimiento, decimal Importe, string? Descripcion);

/// <summary>Cabecera de una póliza contable.</summary>
public sealed record AsientoContableDto(
    int Id, int SucursalId, DateTime FechaUtc, string Tipo, string? Numeracion,
    string? Concepto, List<DetalleAsientoDto> Detalles);

// ---------- Request: registrar una póliza ----------

public sealed record RegistrarAsientoRequest(
    int SucursalId, int UsuarioId, string Tipo, string? Concepto,
    List<RegistrarDetalleAsientoRequest> Detalles);

public sealed record RegistrarDetalleAsientoRequest(
    int CuentaId, int TipoMovimiento, decimal Importe, string? Descripcion);

/// <summary>Resultado al registrar una póliza (garantiza equilibrio débitos = créditos).</summary>
public sealed record RegistrarAsientoResult(
    int AsientoId, string Numeracion, List<DetalleAsientoDto> Detalles, decimal TotalDebitos, decimal TotalCreditos,
    bool Equilibrado);

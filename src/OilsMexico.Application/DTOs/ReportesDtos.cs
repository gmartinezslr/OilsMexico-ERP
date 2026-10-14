namespace OilsMexico.Application.DTOs;

// ---------- Exportación Excel/PDF (checklist §2.1) ----------

/// <summary>Archivo generado para descarga (Excel o PDF).</summary>
public sealed record ArchivoExportadoDto(
    string NombreArchivo, string MimeTipo, byte[] Contenido);

// ---------- Reportes financieros (checklist §1.4) ----------

/// <summary>Renglón de balanza de comprobación (movimientos del periodo + saldo).</summary>
public sealed record BalanzaRenglonDto(
    int CuentaId, string Codigo, string Nombre, int Tipo,
    decimal Debitos, decimal Creditos, decimal SaldoDeudor, decimal SaldoAcreedor);

/// <summary>Balanza de comprobación de un periodo.</summary>
public sealed record BalanzaComprobacionDto(
    int SucursalId, DateTime Desde, DateTime Hasta,
    List<BalanzaRenglonDto> Renglones,
    decimal TotalDebitos, decimal TotalCreditos,
    decimal TotalDeudor, decimal TotalAcreedor);

/// <summary>Sección del Balance General (Activo/Pasivo/Patrimonio con sus cuentas).</summary>
public sealed record BalanceSeccionDto(
    string Titulo, List<BalanceCuentaDto> Cuentas, decimal Total);

/// <summary>Cuenta del balance con su saldo.</summary>
public sealed record BalanceCuentaDto(
    string Codigo, string Nombre, decimal Saldo);

/// <summary>Balance General: Activo = Pasivo + Patrimonio.</summary>
public sealed record BalanceGeneralDto(
    int SucursalId, DateTime Corte,
    BalanceSeccionDto Activo, BalanceSeccionDto Pasivo, BalanceSeccionDto Patrimonio,
    decimal TotalActivo, decimal TotalPasivoPatrimonio, bool Cuadrado);

/// <summary>Estado de Resultados por sucursal y periodo.</summary>
public sealed record EstadoResultadosDto(
    int SucursalId, DateTime Desde, DateTime Hasta,
    decimal Ventas, decimal Devoluciones, decimal VentasNetas,
    decimal CostoVentas, decimal UtilidadBruta, decimal GastosOperativos,
    decimal UtilidadOperacion, decimal IvaTrasladado, decimal UtilidadNeta,
    decimal MargenBrutoPct, decimal MargenNetoPct);

/// <summary>Renglón del informe de cambio de existencias.</summary>
public sealed record ExistenciaRenglonDto(
    int ProductoId, string Sku, string Nombre,
    decimal Inicial, decimal Compras, decimal Ventas,
    decimal Ajustes, decimal Traspasos, decimal Final);

/// <summary>Informe de cambio de existencias del periodo.</summary>
public sealed record CambioExistenciasDto(
    int SucursalId, DateTime Desde, DateTime Hasta,
    List<ExistenciaRenglonDto> Renglones,
    decimal TotalInicial, decimal TotalCompras, decimal TotalVentas,
    decimal TotalAjustes, decimal TotalTraspasos, decimal TotalFinal);

/// <summary>Indicadores por sucursal y periodo (margen, rotación, días de inventario).</summary>
public sealed record IndicadoresSucursalDto(
    int SucursalId, string Sucursal, DateTime Desde, DateTime Hasta,
    decimal VentasNetas, decimal UtilidadBruta, decimal MargenPct,
    decimal RotacionInventario, decimal DiasInventario, decimal TicketPromedio,
    int NumVentas, decimal InventarioPromedio);

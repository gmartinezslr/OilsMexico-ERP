namespace OilsMexico.Domain.Entities;

using OilsMexico.Domain.Enums;

/// <summary>
/// Cuenta bancaria de la empresa (el "libro" contra el que se concilia el extracto del banco).
/// Pertenece a una sucursal; el saldo del ERP se contrasta con el saldo del estado de cuenta.
/// Tabla: cuentas_bancarias.
/// </summary>
public sealed class CuentaBancaria
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    /// <summary>Nombre del banco (BBVA, Banamex, Santander…).</summary>
    public string Banco { get; set; } = string.Empty;
    /// <summary>Alias legible de la cuenta («Cuenta principal», «Nómina»…).</summary>
    public string Alias { get; set; } = string.Empty;
    /// <summary>Número de cuenta (interno del banco); no sensible para conciliación.</summary>
    public string? NumeroCuenta { get; set; }
    /// <summary>CLABE interbancaria (18 dígitos) para empatar transferencias SPEI.</summary>
    public string? Clabe { get; set; }
    public string Moneda { get; set; } = "MXN";
    /// <summary>Saldo con el que se abrió la cuenta; base del saldo calculado del extracto.</summary>
    public decimal SaldoInicial { get; set; }
    public bool Activa { get; set; } = true;
    public List<MovimientoBancario> Movimientos { get; set; } = [];
}

/// <summary>
/// Renglón del estado de cuenta bancario (extracto) capturado en el ERP.
/// Nace <see cref="EstadoConciliacion.Pendiente"/> y se marca
/// <see cref="EstadoConciliacion.Conciliado"/> al empatarlo con un
/// <see cref="VentaCobro"/> (Abono: entró dinero) o un <see cref="CompraPago"/>
/// (Cargo: salió dinero). Tabla: movimientos_bancarios.
/// </summary>
public sealed class MovimientoBancario
{
    public int Id { get; set; }
    public int CuentaBancariaId { get; set; }
    public CuentaBancaria? CuentaBancaria { get; set; }
    /// <summary>Fecha valor del movimiento según el banco.</summary>
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Concepto { get; set; } = string.Empty;
    /// <summary>Folio/rastreo SPEI, número de cheque u otra referencia del extracto.</summary>
    public string? Referencia { get; set; }
    /// <summary>Abono = entró dinero a la cuenta; Cargo = salió dinero.</summary>
    public NaturalezaMovimiento Naturaleza { get; set; } = NaturalezaMovimiento.Abono;
    public decimal Monto { get; set; }
    public EstadoConciliacion Estado { get; set; } = EstadoConciliacion.Pendiente;

    // Vínculo con la operación del ERP: EXACTAMENTE uno de los dos cuando está conciliado.
    // Abono -> VentaCobro; Cargo -> CompraPago. Se conserva nulo mientras esté pendiente.
    public int? VentaCobroId { get; set; }
    public VentaCobro? VentaCobro { get; set; }
    public int? CompraPagoId { get; set; }
    public CompraPago? CompraPago { get; set; }

    public DateTime? ConciliadoUtc { get; set; }
    public int? ConciliadoPorUsuarioId { get; set; }
    public string? Notas { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
}

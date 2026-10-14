namespace OilsMexico.Domain.Entities;

/// <summary>
/// Bandera de periodo contable cerrado por sucursal (cierre mensual §1.3).
/// Mientras exista un registro para (sucursal, año, mes) no se aceptan asientos
/// fechados en ese periodo; la reapertura es solo Admin y elimina el registro.
/// </summary>
public sealed class PeriodoContable
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; } // 1-12 (mes local de negocio)
    public int UsuarioCierreId { get; set; }
    public DateTime CerradoUtc { get; set; } = DateTime.UtcNow;
    public string? Notas { get; set; }
}
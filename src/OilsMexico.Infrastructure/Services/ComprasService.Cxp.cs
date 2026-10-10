using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed partial class ComprasService
{
    public async Task<CompraResult> CancelarAsync(int compraId, string motivo, CancellationToken ct = default)
    {
        var compra = await db.Compras.Include(c => c.Detalles)
            .FirstOrDefaultAsync(c => c.Id == compraId, ct)
            ?? throw new InvalidOperationException("Compra no existe.");
        if (ctx.Rol != "Admin" && compra.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes cancelar compras de otra sucursal.");
        if (compra.Estado == EstadoCompra.Recibida)
            throw new InvalidOperationException("Ya fue recibida; registra una devolución en Almacén.");
        if (compra.Estado == EstadoCompra.Cancelada) throw new InvalidOperationException("Ya está cancelada.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new InvalidOperationException("El motivo es obligatorio.");
        compra.Estado = EstadoCompra.Cancelada;
        compra.Notas = string.IsNullOrWhiteSpace(compra.Notas)
            ? $"Cancelada: {motivo.Trim()}" : $"{compra.Notas} | Cancelada: {motivo.Trim()}";
        await db.SaveChangesAsync(ct);
        return new CompraResult(compra.Id, compra.FolioInterno, compra.Subtotal, compra.Iva, compra.Total, compra.Estado.ToString());
    }

    public async Task<CompraResult> RegistrarPagoAsync(PagoCompraRequest req, CancellationToken ct = default)
    {
        var compra = await db.Compras.FirstOrDefaultAsync(c => c.Id == req.CompraId, ct)
            ?? throw new InvalidOperationException("Compra no existe.");
        if (ctx.Rol != "Admin" && compra.SucursalId != ctx.SucursalId)
            throw new UnauthorizedAccessException("No puedes pagar compras de otra sucursal.");
        if (compra.Estado == EstadoCompra.Cancelada) throw new InvalidOperationException("La compra está cancelada.");
        if (req.Monto <= 0) throw new InvalidOperationException("El monto debe ser mayor a cero.");
        var saldo = compra.Total - compra.MontoPagado;
        if (req.Monto - saldo > 0.01m)
            throw new InvalidOperationException("El monto excede el saldo.");
        db.CompraPagos.Add(new CompraPago
        {
            CompraId = compra.Id, Monto = Math.Round(req.Monto, 2),
            FormaPago = req.FormaPago, Referencia = req.Referencia, UsuarioId = ctx.UsuarioId
        });
        compra.MontoPagado = Math.Round(compra.MontoPagado + req.Monto, 2);
        compra.EstadoPago = compra.Total - compra.MontoPagado <= 0.01m ? EstadoPagoCompra.Pagada
            : compra.MontoPagado > 0 ? EstadoPagoCompra.Parcial : EstadoPagoCompra.Pendiente;
        await db.SaveChangesAsync(ct);
        return new CompraResult(compra.Id, compra.FolioInterno, compra.Subtotal, compra.Iva, compra.Total, compra.EstadoPago.ToString());
    }
}

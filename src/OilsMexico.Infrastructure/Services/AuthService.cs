using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>Login por PIN (heredado del PHP: 1234/1111/2222/3333). Compara hash SHA-256.</summary>
public sealed class AuthService(ErpDbContext db) : IAuthService
{
    public async Task<SesionDto?> LoginPorPinAsync(string pin, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pin.Trim())));
        var u = await db.Usuarios.Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.PinHash == hash && x.Activo, ct);
        if (u is null) return null;
        return new SesionDto(u.Id, u.Nombre, u.Rol, u.SucursalId, u.Sucursal?.Nombre ?? "");
    }
}

using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Catálogos maestros (productos, sucursales, unidades, usuarios). Reemplaza el acceso
/// directo a <c>ErpDbContext</c> que hacían las páginas Blazor: ahora la web depende de
/// <c>ICatalogosService</c> (DIP) y el acceso a datos de cada entidad vive en un solo
/// método reutilizable (SRP). La página arma el DTO ya saneado (normalización de SKU,
/// código de barras, etc.) y este servicio sólo valida reglas de BD (duplicados, FKs).
/// </summary>
public sealed class CatalogosService(ErpDbContext db) : ICatalogosService
{
    // ------------------------------------------------------------------
    // Productos
    // ------------------------------------------------------------------
    public async Task<List<ProductoCatalogoDto>> BuscarProductosAsync(string? filtro, CancellationToken ct = default)
    {
        var q = db.Productos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(p => p.Sku.ToLower().Contains(f) || p.Nombre.ToLower().Contains(f)
                || p.Marca.ToLower().Contains(f) || p.Viscosidad.ToLower().Contains(f));
        }
        return await q.OrderBy(p => p.Nombre).Take(200).Select(p => A(p)).ToListAsync(ct);
    }

    public async Task<ProductoCatalogoDto?> ObtenerProductoAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : A(p);
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarProductoAsync(ProductoCatalogoDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Sku) || string.IsNullOrWhiteSpace(dto.Nombre)
            || string.IsNullOrWhiteSpace(dto.Marca) || string.IsNullOrWhiteSpace(dto.Viscosidad))
            return (false, "SKU, nombre, marca y viscosidad son obligatorios.", 0);

        var tipoBase = string.IsNullOrWhiteSpace(dto.TipoBase) ? "Sintetico" : dto.TipoBase;
        var piezas = dto.PiezasPorCaja <= 0 ? 1 : dto.PiezasPorCaja;
        var venta = dto.PrecioVenta;
        var unitario = dto.PrecioUnitario;
        if (venta == 0 && unitario > 0) venta = unitario;
        else if (unitario == 0 && venta > 0) unitario = venta;

        if (venta == 0 && dto.PrecioMayoreo == 0 && dto.PrecioLista == 0)
            return (false, "Captura al menos un precio mayor a cero.", 0);

        var dup = await db.Productos.AsNoTracking()
            .AnyAsync(p => p.Sku.ToLower() == dto.Sku.ToLower() && p.Id != dto.Id, ct);
        if (dup) return (false, "Ya existe otro producto con ese SKU.", 0);

        if (dto.Id == 0)
        {
            var p = new Producto
            {
                Sku = dto.Sku, Nombre = dto.Nombre, Marca = dto.Marca, Viscosidad = dto.Viscosidad,
                TipoBase = tipoBase, DescripcionTecnica = dto.DescripcionTecnica, Categoria = dto.Categoria,
                SkuAnterior = dto.SkuAnterior, Sae = dto.Sae, Especificacion = dto.Especificacion,
                PiezasPorCaja = piezas, PrecioLista = dto.PrecioLista,
                PrecioLpOroConIva = dto.PrecioLpOroConIva, PrecioUnitario = unitario,
                PrecioVenta = venta, PrecioMayoreo = dto.PrecioMayoreo, PrecioCosto = dto.PrecioCosto,
                Activo = true
            };
            db.Productos.Add(p);
            await db.SaveChangesAsync(ct);
            // Crea stock en cero por sucursal + presentación base "Litro x1".
            foreach (var sid in await db.Sucursales.Select(s => s.Id).ToListAsync(ct))
                db.InventarioSucursal.Add(new InventarioSucursal
                { SucursalId = sid, ProductoId = p.Id, LoteId = null, StockActual = 0, StockMinimo = 5 });
            db.UnidadesMedida.Add(new UnidadMedida
            {
                ProductoId = p.Id, UnidadNombre = "Litro", FactorConversion = 1m,
                PrecioUnitario = venta > 0 ? venta : dto.PrecioMayoreo
            });
            await db.SaveChangesAsync(ct);
            return (true, "Producto creado (con Litro x1 y stock en sucursales).", p.Id);
        }

        var upd = await db.Productos.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "El producto ya no existe.", 0);
        upd.Sku = dto.Sku; upd.Nombre = dto.Nombre; upd.Marca = dto.Marca; upd.Viscosidad = dto.Viscosidad;
        upd.TipoBase = tipoBase; upd.DescripcionTecnica = dto.DescripcionTecnica; upd.Categoria = dto.Categoria;
        upd.SkuAnterior = dto.SkuAnterior; upd.Sae = dto.Sae; upd.Especificacion = dto.Especificacion;
        upd.PiezasPorCaja = piezas; upd.PrecioLista = dto.PrecioLista;
        upd.PrecioLpOroConIva = dto.PrecioLpOroConIva; upd.PrecioUnitario = unitario;
        upd.PrecioVenta = venta; upd.PrecioMayoreo = dto.PrecioMayoreo; upd.PrecioCosto = dto.PrecioCosto;
        await db.SaveChangesAsync(ct);
        return (true, "Producto guardado correctamente.", upd.Id);
    }

    public async Task<(bool Ok, string Mensaje)> CambiarEstadoProductoAsync(int id, bool activo, CancellationToken ct = default)
    {
        var p = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return (false, "El producto ya no existe.");
        p.Activo = activo;
        await db.SaveChangesAsync(ct);
        return (true, activo ? "Producto reactivado." : "Producto desactivado (ya no aparece en POS).");
    }

    // ------------------------------------------------------------------
    // Presentaciones (unidades de venta)
    // ------------------------------------------------------------------
    public async Task<List<UnidadMedidaDto>> ListarUnidadesAsync(int productoId, CancellationToken ct = default)
        => await db.UnidadesMedida.AsNoTracking().Where(u => u.ProductoId == productoId)
            .OrderBy(u => u.Id).Select(u => U(u)).ToListAsync(ct);

    public async Task<UnidadMedidaDto?> ObtenerUnidadAsync(int id, CancellationToken ct = default)
    {
        var u = await db.UnidadesMedida.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return u is null ? null : U(u);
    }

    public async Task<List<StockSucursalDto>> StockPorSucursalAsync(int productoId, CancellationToken ct = default)
    {
        var rows = await (from i in db.InventarioSucursal.AsNoTracking()
                          join s in db.Sucursales.AsNoTracking() on i.SucursalId equals s.Id
                          where i.ProductoId == productoId
                          group new { i, s } by s.Nombre into g
                          select new { Suc = g.Key, Stock = g.Sum(x => x.i.StockActual), Min = g.Sum(x => x.i.StockMinimo) })
            .ToListAsync(ct);
        return rows.Select(r => new StockSucursalDto(r.Suc, r.Stock, r.Min, r.Min > 0 && r.Stock <= r.Min)).ToList();
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarUnidadAsync(UnidadMedidaDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UnidadNombre))
            return (false, "La unidad es obligatoria.", 0);
        if (dto.FactorConversion <= 0)
            return (false, "Litros por unidad debe ser mayor a cero.", 0);
        if (dto.PrecioUnitario < 0)
            return (false, "El precio no puede ser negativo.", 0);

        var barra = CodigosBarra.Normalizar(dto.CodigoBarra);
        var (barraOk, barraMsg) = CodigosBarra.Validar(barra);
        if (!barraOk) return (false, barraMsg, 0);
        if (!string.IsNullOrEmpty(barra) && await db.UnidadesMedida.AsNoTracking()
            .AnyAsync(u => u.CodigoBarra == barra && u.Id != dto.Id, ct))
            return (false, "Ese codigo de barras ya esta en uso.", 0);

        if (dto.Id == 0)
        {
            var u = new UnidadMedida
            {
                ProductoId = dto.ProductoId, UnidadNombre = dto.UnidadNombre,
                FactorConversion = dto.FactorConversion, CodigoBarra = barra, PrecioUnitario = dto.PrecioUnitario
            };
            db.UnidadesMedida.Add(u);
            await db.SaveChangesAsync(ct);
            return (true, "Presentacion guardada.", u.Id);
        }

        var upd = await db.UnidadesMedida.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "La presentacion ya no existe.", 0);
        upd.ProductoId = dto.ProductoId; upd.UnidadNombre = dto.UnidadNombre;
        upd.FactorConversion = dto.FactorConversion; upd.CodigoBarra = barra; upd.PrecioUnitario = dto.PrecioUnitario;
        await db.SaveChangesAsync(ct);
        return (true, "Presentacion guardada.", upd.Id);
    }

    public async Task<(bool Ok, string Mensaje)> EliminarUnidadAsync(int id, CancellationToken ct = default)
    {
        var u = await db.UnidadesMedida.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return (false, "La presentacion ya no existe.");
        var totales = await db.UnidadesMedida.CountAsync(x => x.ProductoId == u.ProductoId, ct);
        if (totales <= 1) return (false, "El producto debe tener al menos una presentacion.");
        var enVentas = await db.Set<FacturaDetalle>().AsNoTracking().AnyAsync(d => d.UnidadMedidaId == id, ct);
        if (enVentas) return (false, "No se puede quitar: ya se uso en ventas.");
        db.UnidadesMedida.Remove(u);
        await db.SaveChangesAsync(ct);
        return (true, "Presentacion eliminada.");
    }

    // ------------------------------------------------------------------
    // Sucursales
    // ------------------------------------------------------------------
    public async Task<List<SucursalDto>> ListarSucursalesAsync(CancellationToken ct = default)
        => await db.Sucursales.AsNoTracking().OrderBy(s => s.CodigoSucursal).Select(s => S(s)).ToListAsync(ct);

    public async Task<SucursalDto?> ObtenerSucursalAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Sucursales.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : S(s);
    }

    public async Task<List<SucursalResumenDto>> ListarSucursalesResumenAsync(CancellationToken ct = default)
        => await db.Sucursales.AsNoTracking().OrderBy(s => s.CodigoSucursal)
            .Select(s => new SucursalResumenDto(s.Id, s.Nombre, s.CodigoSucursal)).ToListAsync(ct);

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarSucursalAsync(SucursalDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) return (false, "El nombre es obligatorio.", 0);
        var codigo = (dto.CodigoSucursal ?? "").Trim().ToUpperInvariant();
        if (codigo.Length == 0 || codigo.Length > 10)
            return (false, "Codigo de sucursal obligatorio (max. 10 caracteres).", 0);
        var rfc = (dto.RfcEmisor ?? "").Trim().ToUpperInvariant();
        if (rfc.Length is < 12 or > 13)
            return (false, "RFC del emisor debe tener 12 (moral) o 13 (fisica) caracteres.", 0);
        var cp = (dto.CodigoPostal ?? "").Trim();
        if (cp.Length != 5 || !cp.All(char.IsDigit))
            return (false, "Codigo postal del domicilio fiscal debe tener 5 digitos (usa el buscador SEPOMEX).", 0);
        if (string.IsNullOrWhiteSpace(dto.Colonia))
            return (false, "Selecciona la colonia del catalogo SEPOMEX.", 0);

        var dup = await db.Sucursales.AnyAsync(s => s.CodigoSucursal == codigo && s.Id != dto.Id, ct);
        if (dup) return (false, $"Ya existe otra sucursal con el codigo '{codigo}'.", 0);

        var razon = string.IsNullOrWhiteSpace(dto.RazonSocial) ? dto.Nombre : dto.RazonSocial;
        var direccion = $"{dto.Calle} {dto.NumeroExterior} {dto.NumeroInterior}, {dto.Colonia}, CP {cp}, {dto.Municipio}, {dto.Estado}".Trim(' ', ',');

        if (dto.Id == 0)
        {
            var s = new Sucursal
            {
                Nombre = dto.Nombre, CodigoSucursal = codigo, Direccion = direccion, RfcEmisor = rfc,
                Activa = dto.Activa, RazonSocial = razon, RegimenFiscal = dto.RegimenFiscal, CodigoPostal = cp,
                Calle = dto.Calle, NumeroExterior = dto.NumeroExterior, NumeroInterior = dto.NumeroInterior,
                Colonia = dto.Colonia, Municipio = dto.Municipio, Estado = dto.Estado, Ciudad = dto.Ciudad,
                Pais = dto.Pais, Telefono = dto.Telefono, Email = dto.Email
            };
            db.Sucursales.Add(s);
            await db.SaveChangesAsync(ct);
            return (true, $"Sucursal '{s.Nombre}' guardada.", s.Id);
        }

        var upd = await db.Sucursales.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "La sucursal ya no existe.", 0);
        upd.Nombre = dto.Nombre; upd.CodigoSucursal = codigo; upd.Direccion = direccion; upd.RfcEmisor = rfc;
        upd.Activa = dto.Activa; upd.RazonSocial = razon; upd.RegimenFiscal = dto.RegimenFiscal; upd.CodigoPostal = cp;
        upd.Calle = dto.Calle; upd.NumeroExterior = dto.NumeroExterior; upd.NumeroInterior = dto.NumeroInterior;
        upd.Colonia = dto.Colonia; upd.Municipio = dto.Municipio; upd.Estado = dto.Estado; upd.Ciudad = dto.Ciudad;
        upd.Pais = dto.Pais; upd.Telefono = dto.Telefono; upd.Email = dto.Email;
        await db.SaveChangesAsync(ct);
        return (true, $"Sucursal '{upd.Nombre}' guardada.", upd.Id);
    }

    // ------------------------------------------------------------------
    // Usuarios
    // ------------------------------------------------------------------
    public async Task<List<UsuarioRowDto>> ListarUsuariosAsync(string? filtro, bool soloActivos, CancellationToken ct = default)
    {
        var q = from u in db.Usuarios.AsNoTracking()
                join s in db.Sucursales.AsNoTracking() on u.SucursalId equals s.Id into sj
                from s in sj.DefaultIfEmpty()
                orderby u.Nombre
                select new { u, Suc = s != null ? s.Nombre : "-" };
        if (soloActivos) q = q.Where(x => x.u.Activo);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim().ToLower();
            q = q.Where(x => x.u.Nombre.ToLower().Contains(f) || x.u.Rol.ToLower().Contains(f)
                || x.Suc.ToLower().Contains(f));
        }
        var ahora = DateTime.UtcNow;
        return await q.Take(200).Select(x => new UsuarioRowDto(
            x.u.Id, x.u.Nombre, x.u.Rol, x.Suc, x.u.Activo,
            x.u.BloqueadoDefinitivamente, x.u.BloqueadoHastaUtc != null && x.u.BloqueadoHastaUtc > ahora)).ToListAsync(ct);
    }

    public async Task<UsuarioDto?> ObtenerUsuarioAsync(int id, CancellationToken ct = default)
    {
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return null;
        return new UsuarioDto(u.Id, u.Nombre, u.Correo, u.PinHash, u.PasswordHash, u.Rol, u.RoleId,
            u.SucursalId, u.Activo, u.IntentosFallidos, u.BloqueosTemporales, u.BloqueadoHastaUtc,
            u.BloqueadoDefinitivamente, u.SesionToken, u.DosFaActivo, u.DosFaSecret, u.UltimoCambioPasswordUtc);
    }

    public async Task<(bool Ok, string Mensaje, int Id)> GuardarUsuarioAsync(UsuarioDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) return (false, "El nombre es obligatorio.", 0);
        if (string.IsNullOrWhiteSpace(dto.Correo))
        {
            if (dto.Id == 0) return (false, "El correo es obligatorio para un usuario nuevo.", 0);
        }
        else if (await db.Usuarios.AnyAsync(x => x.Id != dto.Id && x.Correo != null && x.Correo.ToLower() == dto.Correo.ToLower(), ct))
            return (false, "Ese correo ya esta asignado a otro usuario.", 0);

        if (!await db.Sucursales.AnyAsync(s => s.Id == dto.SucursalId, ct))
            return (false, "Selecciona una sucursal valida.", 0);
        if (dto.Id == 0 && string.IsNullOrEmpty(dto.PasswordHash))
            return (false, "La contraseña es obligatoria.", 0);

        if (dto.Id == 0)
        {
            var u = new Usuario
            {
                Nombre = dto.Nombre, Correo = dto.Correo, PinHash = dto.PinHash, PasswordHash = dto.PasswordHash,
                Rol = dto.Rol, RoleId = dto.RoleId, SucursalId = dto.SucursalId, Activo = dto.Activo,
                DosFaActivo = dto.DosFaActivo, DosFaSecret = dto.DosFaSecret,
                UltimoCambioPasswordUtc = dto.UltimoCambioPasswordUtc
            };
            db.Usuarios.Add(u);
            await db.SaveChangesAsync(ct);
            return (true, $"Usuario '{u.Nombre}' guardado.", u.Id);
        }

        var upd = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
        if (upd is null) return (false, "El usuario ya no existe.", 0);
        upd.Nombre = dto.Nombre; upd.Correo = dto.Correo; upd.PinHash = dto.PinHash;
        upd.Rol = dto.Rol; upd.RoleId = dto.RoleId; upd.SucursalId = dto.SucursalId; upd.Activo = dto.Activo;
        if (!string.IsNullOrEmpty(dto.PasswordHash))
        {
            upd.PasswordHash = dto.PasswordHash;
            upd.UltimoCambioPasswordUtc = dto.UltimoCambioPasswordUtc ?? upd.UltimoCambioPasswordUtc;
            upd.SesionToken = null; // Revoca sesiones abiertas al cambiar la contraseña.
        }
        await db.SaveChangesAsync(ct);
        return (true, $"Usuario '{upd.Nombre}' guardado.", upd.Id);
    }

    public async Task<(bool Ok, string Mensaje)> CambiarEstadoUsuarioAsync(int id, bool activo, CancellationToken ct = default)
    {
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return (false, "El usuario ya no existe.");
        u.Activo = activo;
        if (!activo) u.SesionToken = null; // Revoca las sesiones abiertas del usuario desactivado.
        await db.SaveChangesAsync(ct);
        return (true, activo ? "Usuario reactivado." : "Usuario desactivado (ya no puede iniciar sesion).");
    }

    public async Task<List<VendedorDto>> ListarVendedoresAsync(CancellationToken ct = default)
        => await db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && (u.Rol == Roles.Vendedor || u.Rol == Roles.Admin))
            .OrderBy(u => u.Nombre)
            .Select(u => new VendedorDto(u.Id, u.Nombre)).ToListAsync(ct);

    // ------------------------------------------------------------------
    // Proyecciones entity -> DTO
    // ------------------------------------------------------------------
    private static ProductoCatalogoDto A(Producto p) => new(p.Id, p.Sku, p.Nombre, p.Marca, p.Viscosidad,
        p.TipoBase, p.DescripcionTecnica, p.Categoria, p.SkuAnterior, p.Sae, p.Especificacion,
        p.PiezasPorCaja, p.PrecioLista, p.PrecioLpOroConIva, p.PrecioUnitario, p.PrecioVenta,
        p.PrecioMayoreo, p.PrecioCosto, p.Activo);

    private static UnidadMedidaDto U(UnidadMedida u) => new(u.Id, u.ProductoId, u.UnidadNombre,
        u.FactorConversion, u.CodigoBarra, u.PrecioUnitario);

    private static SucursalDto S(Sucursal s) => new(s.Id, s.Nombre, s.CodigoSucursal, s.Direccion,
        s.RfcEmisor, s.Activa, s.RazonSocial, s.RegimenFiscal, s.CodigoPostal, s.Calle,
        s.NumeroExterior, s.NumeroInterior, s.Colonia, s.Municipio, s.Estado, s.Ciudad, s.Pais,
        s.Telefono, s.Email);
}

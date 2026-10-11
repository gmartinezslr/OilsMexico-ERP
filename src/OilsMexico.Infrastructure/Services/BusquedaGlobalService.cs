using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

/// <summary>
/// Búsqueda global del ERP: un solo punto que recorre clientes, productos,
/// proveedores, ventas (por folio) y compras (por folio/id). Top 8 por módulo
/// para no saturar la UI. Sin tabla nueva: lee las existentes con AsNoTracking.
/// </summary>
public sealed class BusquedaGlobalService(ErpDbContext db) : IBusquedaGlobalService
{
    public async Task<List<BusquedaGlobalDto>> BuscarAsync(string texto, int sucursalId, CancellationToken ct = default)
    {
        var res = new List<BusquedaGlobalDto>();
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2) return res;
        var f = texto.Trim().ToLower();

        var clientes = await db.Clientes.AsNoTracking()
            .Where(c => c.Activo && (c.Nombre.ToLower().Contains(f) || c.Rfc.ToLower().Contains(f)))
            .OrderBy(c => c.Nombre).Take(8)
            .Select(c => new BusquedaGlobalDto("Clientes", c.Nombre, $"{c.Rfc} · {c.CodigoPostal} {c.Colonia}", $"/clientes", c.SituacionFiscal.ToString()))
            .ToListAsync(ct);
        res.AddRange(clientes);

        var productos = await db.Productos.AsNoTracking()
            .Where(p => p.Activo && (p.Sku.ToLower().Contains(f) || p.Nombre.ToLower().Contains(f)
                || p.Marca.ToLower().Contains(f) || p.Viscosidad.ToLower().Contains(f)))
            .OrderBy(p => p.Nombre).Take(8)
            .Select(p => new BusquedaGlobalDto("Productos", $"{p.Sku} — {p.Nombre}", $"{p.Marca} · {p.Viscosidad}", "/productos", p.Categoria))
            .ToListAsync(ct);
        res.AddRange(productos);

        var proveedores = await db.Proveedores.AsNoTracking()
            .Where(p => p.Activo && (p.Nombre.ToLower().Contains(f) || p.Rfc.ToLower().Contains(f)))
            .OrderBy(p => p.Nombre).Take(8)
            .Select(p => new BusquedaGlobalDto("Proveedores", p.Nombre, $"{p.Rfc} · {p.CodigoPostal} {p.Colonia}", "/proveedores"))
            .ToListAsync(ct);
        res.AddRange(proveedores);

        var ventas = await db.Facturas.AsNoTracking()
            .Where(v => v.SucursalId == sucursalId && v.FolioInterno.ToLower().Contains(f))
            .OrderByDescending(v => v.Id).Take(8)
            .Select(v => new BusquedaGlobalDto("Ventas", v.FolioInterno, $"${v.Total:N2} · {v.Estado}", "/historial-ventas", v.Estado.ToString()))
            .ToListAsync(ct);
        res.AddRange(ventas);

        var compras = await db.Compras.AsNoTracking()
            .Where(c => c.SucursalId == sucursalId && (c.FolioInterno.ToLower().Contains(f) || c.Proveedor!.Nombre.ToLower().Contains(f)))
            .OrderByDescending(c => c.Id).Take(8)
            .Select(c => new BusquedaGlobalDto("Compras", c.FolioInterno, $"{c.Proveedor!.Nombre} · ${c.Total:N2}", "/compras", c.Estado.ToString()))
            .ToListAsync(ct);
        res.AddRange(compras);

        return res;
    }

    public List<PlantillaMensajeDto> ListarPlantillas() =>
    [
        new("cobro-whatsapp", "Cobro por WhatsApp", "WhatsApp",
            "Hola {Nombre}, le recordamos el saldo de la factura {Folio} por ${Total} al {Fecha}. {Empresa} agradece su pago."),
        new("cobro-correo", "Cobro por correo", "Correo",
            "Estimado {Nombre}: la factura {Folio} por ${Total} del {Fecha} sigue pendiente de pago. Quedamos atentos. — {Empresa}"),
        new("entrega", "Aviso de entrega", "WhatsApp",
            "Hola {Nombre}, su pedido {Folio} (${Total}) está listo / en ruta. Fecha: {Fecha}. — {Empresa}"),
        new("bienvenida", "Bienvenida a cliente", "WhatsApp",
            "Bienvenido {Nombre} a {Empresa}. Su cuenta quedó registrada el {Fecha}. Gracias por su preferencia."),
        new("ticket", "Nota de ticket impreso", "Impresión",
            "{Empresa} · Folio {Folio} · {Fecha} · Total ${Total}. ¡Gracias por su compra, {Nombre}!"),
    ];

    public string RellenarPlantilla(string cuerpo, string? nombre, string? folio, decimal? total, DateTime? fecha, string? empresa)
        => (cuerpo ?? "")
            .Replace("{Nombre}", nombre ?? "—")
            .Replace("{Folio}", folio ?? "—")
            .Replace("{Total}", total.HasValue ? total.Value.ToString("N2") : "—")
            .Replace("{Fecha}", fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy") : DateTime.Now.ToString("dd/MM/yyyy"))
            .Replace("{Empresa}", empresa ?? "OilsMexico");
}

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Application.DTOs;
using OilsMexico.Application.Interfaces;
using OilsMexico.Domain.Entities;
using OilsMexico.Domain.Enums;
using OilsMexico.Domain.Services;
using OilsMexico.Infrastructure.Persistence;

namespace OilsMexico.Infrastructure.Services;

public sealed class DiferenciadoresService(ErpDbContext db, ISucursalContext ctx) : IDiferenciadoresService
{
    private static readonly XNamespace Cfdi = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace Cp = "http://www.sat.gob.mx/CartaPorte31";

    private void ExigirSesion()
    {
        if (!ctx.Establecida) throw new UnauthorizedAccessException("Inicia sesión para operar este módulo.");
    }

    private void ExigirRoles(params string[] roles)
    {
        ExigirSesion();
        if (!roles.Contains(ctx.Rol, StringComparer.Ordinal))
            throw new UnauthorizedAccessException("Tu rol no puede modificar este catálogo.");
    }

    private int SucursalActual()
    {
        ExigirSesion();
        return ctx.ResolverSucursalAutorizada(ctx.SucursalId);
    }

    public async Task<List<ListaPrecioDto>> ListarListasPrecioAsync(CancellationToken ct = default)
    {
        ExigirSesion();
        return await db.ListasPrecio.AsNoTracking().OrderBy(x => x.TipoCliente).ThenBy(x => x.Nombre)
            .Select(x => new ListaPrecioDto(x.Id, x.Nombre, x.TipoCliente, x.Activa)).ToListAsync(ct);
    }

    public async Task<int> GuardarListaPrecioAsync(int id, string nombre, string tipoCliente, bool activa, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin);
        nombre = nombre?.Trim() ?? string.Empty;
        tipoCliente = tipoCliente?.Trim().ToLowerInvariant() ?? string.Empty;
        if (nombre.Length is < 2 or > 100) throw new InvalidOperationException("El nombre de la lista debe tener entre 2 y 100 caracteres.");
        if (tipoCliente is not ("menudeo" or "mayoreo"))
            throw new InvalidOperationException("El tipo debe ser menudeo o mayoreo para conservar compatibilidad con los clientes existentes.");
        var lista = id == 0 ? new ListaPrecio() : await db.ListasPrecio.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La lista de precios no existe.");
        lista.Nombre = nombre;
        lista.TipoCliente = tipoCliente;
        lista.Activa = activa;
        if (activa)
        {
            var previas = await db.ListasPrecio.Where(x => x.TipoCliente == tipoCliente && x.Id != id && x.Activa).ToListAsync(ct);
            foreach (var previa in previas) previa.Activa = false;
        }
        if (id == 0) db.ListasPrecio.Add(lista);
        db.AuditLogs.Add(Auditar("ListaPrecioGuardada", $"Lista #{id} \"{nombre}\" ({tipoCliente}), activa={activa}."));
        await db.SaveChangesAsync(ct);
        return lista.Id;
    }

    public async Task<List<PrecioVolumenDto>> ListarPreciosVolumenAsync(int listaId, CancellationToken ct = default)
    {
        ExigirSesion();
        return await db.PreciosVolumen.AsNoTracking().Where(x => x.ListaPrecioId == listaId)
            .Join(db.Productos.AsNoTracking(), p => p.ProductoId, x => x.Id,
                (p, x) => new PrecioVolumenDto(p.Id, p.ListaPrecioId, x.Id, x.Nombre, p.CantidadMinima, p.PrecioUnitario))
            .OrderBy(x => x.Producto).ThenBy(x => x.CantidadMinima).ToListAsync(ct);
    }

    public async Task GuardarPrecioVolumenAsync(int id, int listaId, int productoId, decimal cantidadMinima, decimal precio, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin);
        if (cantidadMinima <= 0 || precio < 0) throw new InvalidOperationException("La cantidad mínima debe ser positiva y el precio no puede ser negativo.");
        if (!await db.ListasPrecio.AnyAsync(x => x.Id == listaId && x.Activa, ct))
            throw new InvalidOperationException("Selecciona una lista activa.");
        if (!await db.Productos.AnyAsync(x => x.Id == productoId && x.Activo, ct))
            throw new InvalidOperationException("Selecciona un producto activo.");
        var regla = id == 0 ? new PrecioVolumen() : await db.PreciosVolumen.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La regla de precio no existe.");
        regla.ListaPrecioId = listaId;
        regla.ProductoId = productoId;
        regla.CantidadMinima = cantidadMinima;
        regla.PrecioUnitario = precio;
        if (id == 0) db.PreciosVolumen.Add(regla);
        db.AuditLogs.Add(Auditar("PrecioVolumenGuardado",
            $"Regla #{id}: lista {listaId}, producto {productoId}, desde {cantidadMinima:N2} L, precio ${precio:N2}."));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { throw new InvalidOperationException("Ya existe una regla para esa lista, producto y cantidad mínima.", ex); }
    }

    public async Task<PrecioResueltoDto> ResolverPrecioAsync(int productoId, int clienteId, decimal cantidad,
        decimal factorConversion = 1m, CancellationToken ct = default)
    {
        ExigirSesion();
        if (cantidad <= 0 || factorConversion <= 0) throw new InvalidOperationException("La cantidad y su factor de conversión deben ser positivos.");
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clienteId, ct)
            ?? throw new InvalidOperationException("El cliente no existe.");
        var producto = await db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productoId, ct)
            ?? throw new InvalidOperationException("El producto no existe.");
        var lista = await db.ListasPrecio.AsNoTracking()
            .Where(x => x.Activa && x.TipoCliente == cliente.TipoPrecio)
            .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (lista is not null)
        {
            var regla = await db.PreciosVolumen.AsNoTracking()
                .Where(x => x.ListaPrecioId == lista.Id && x.ProductoId == productoId && x.CantidadMinima <= cantidad * factorConversion)
                .OrderByDescending(x => x.CantidadMinima).FirstOrDefaultAsync(ct);
            if (regla is not null) return new PrecioResueltoDto(regla.PrecioUnitario, $"{lista.Nombre} · desde {regla.CantidadMinima:N2}");
        }
        var precioBase = cliente.TipoPrecio == "mayoreo" ? producto.PrecioMayoreo : producto.PrecioVenta;
        return new PrecioResueltoDto(precioBase, lista is null ? "Precio de catálogo" : $"{lista.Nombre} · precio de catálogo");
    }

    public async Task<Dictionary<int, PrecioResueltoDto>> ResolverPreciosAsync(
        IReadOnlyCollection<int> productoIds, int clienteId,
        IReadOnlyDictionary<int, decimal>? factoresConversion = null, CancellationToken ct = default)
    {
        ExigirSesion();
        if (productoIds.Count == 0) return [];
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clienteId, ct)
            ?? throw new InvalidOperationException("El cliente no existe.");
        var productos = await db.Productos.AsNoTracking().Where(x => productoIds.Contains(x.Id))
            .ToListAsync(ct);
        var lista = await db.ListasPrecio.AsNoTracking()
            .Where(x => x.Activa && x.TipoCliente == cliente.TipoPrecio)
            .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        var reglas = lista is null ? [] : await db.PreciosVolumen.AsNoTracking()
            .Where(x => x.ListaPrecioId == lista.Id && productoIds.Contains(x.ProductoId))
            .OrderByDescending(x => x.CantidadMinima).ToListAsync(ct);
        var mejorRegla = reglas.GroupBy(x => x.ProductoId).ToDictionary(
            g => g.Key,
            g => g.FirstOrDefault(r => r.CantidadMinima <= (factoresConversion?.GetValueOrDefault(g.Key) ?? 1m)));
        return productos.ToDictionary(x => x.Id, x =>
        {
            if (mejorRegla.TryGetValue(x.Id, out var regla) && regla is not null)
                return new PrecioResueltoDto(regla.PrecioUnitario, $"{lista!.Nombre} · desde {regla.CantidadMinima:N2}");
            var basePrecio = cliente.TipoPrecio == "mayoreo" ? x.PrecioMayoreo : x.PrecioVenta;
            return new PrecioResueltoDto(basePrecio, lista is null ? "Precio de catálogo" : $"{lista.Nombre} · precio de catálogo");
        });
    }

    public async Task<List<CompatibilidadVehiculoDto>> BuscarCompatibilidadAsync(string marca, string modelo, int anio, CancellationToken ct = default)
    {
        ExigirSesion();
        marca = marca?.Trim().ToLowerInvariant() ?? string.Empty;
        modelo = modelo?.Trim().ToLowerInvariant() ?? string.Empty;
        if (marca.Length < 2 || modelo.Length < 1 || anio < 1900 || anio > DateTime.UtcNow.Year + 2) return [];
        var filas = await db.CompatibilidadesVehiculo.AsNoTracking()
            .Where(x => x.AnioDesde <= anio && x.AnioHasta >= anio).Take(5000).ToListAsync(ct);
        var productIds = filas.Where(x => x.ProductoId.HasValue).Select(x => x.ProductoId!.Value).Distinct().ToArray();
        var productos = await db.Productos.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        return filas.Where(x => x.Marca.ToLower().Contains(marca)
                && x.Modelo.ToLower().Contains(modelo))
            .Select(x => new CompatibilidadVehiculoDto(x.Id, x.Marca, x.Modelo, x.AnioDesde, x.AnioHasta,
                x.Motor, x.Viscosidad, x.Norma, x.CapacidadLitros, x.ProductoId,
                x.ProductoId is int id ? productos.GetValueOrDefault(id) : null))
            .OrderBy(x => x.Marca).ThenBy(x => x.Modelo).Take(50).ToList();
    }

    public async Task<List<CompatibilidadVehiculoDto>> ListarCompatibilidadesAsync(CancellationToken ct = default)
    {
        ExigirSesion();
        var rows = await db.CompatibilidadesVehiculo.AsNoTracking().OrderBy(x => x.Marca).ThenBy(x => x.Modelo)
            .Take(5000).ToListAsync(ct);
        var productIds = rows.Where(x => x.ProductoId.HasValue).Select(x => x.ProductoId!.Value).Distinct().ToArray();
        var productos = await db.Productos.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        return rows.Select(x => new CompatibilidadVehiculoDto(x.Id, x.Marca, x.Modelo, x.AnioDesde,
            x.AnioHasta, x.Motor, x.Viscosidad, x.Norma, x.CapacidadLitros, x.ProductoId,
            x.ProductoId is int id ? productos.GetValueOrDefault(id) : null)).ToList();
    }

    public async Task GuardarCompatibilidadAsync(int id, CompatibilidadVehiculoRequest req, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Almacen);
        if (string.IsNullOrWhiteSpace(req.Marca) || string.IsNullOrWhiteSpace(req.Modelo)
            || string.IsNullOrWhiteSpace(req.Motor) || string.IsNullOrWhiteSpace(req.Viscosidad)
            || req.Marca.Length > 80 || req.Modelo.Length > 100 || req.Motor.Length > 100
            || req.Viscosidad.Length > 30 || (req.Norma?.Length ?? 0) > 100
            || req.AnioDesde < 1900 || req.AnioHasta < req.AnioDesde || req.AnioHasta > DateTime.UtcNow.Year + 2
            || (req.CapacidadLitros.HasValue && req.CapacidadLitros <= 0))
            throw new InvalidOperationException("Completa marca, modelo, motor, viscosidad y un rango de años/capacidad válido.");
        if (req.ProductoId.HasValue && !await db.Productos.AnyAsync(x => x.Id == req.ProductoId && x.Activo, ct))
            throw new InvalidOperationException("El producto relacionado no existe o está inactivo.");
        var item = id == 0 ? new CompatibilidadVehiculo() : await db.CompatibilidadesVehiculo.FindAsync([id], ct)
            ?? throw new InvalidOperationException("La compatibilidad no existe.");
        item.Marca = req.Marca.Trim(); item.Modelo = req.Modelo.Trim();
        item.AnioDesde = req.AnioDesde; item.AnioHasta = req.AnioHasta;
        item.Motor = req.Motor.Trim(); item.Viscosidad = req.Viscosidad.Trim();
        item.Norma = string.IsNullOrWhiteSpace(req.Norma) ? null : req.Norma.Trim();
        item.CapacidadLitros = req.CapacidadLitros; item.ProductoId = req.ProductoId;
        if (id == 0) db.CompatibilidadesVehiculo.Add(item);
        db.AuditLogs.Add(Auditar("CompatibilidadGuardada",
            $"{item.Marca} {item.Modelo} {item.AnioDesde}-{item.AnioHasta}, motor {item.Motor}, {item.Viscosidad}."));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<EvaluacionProveedorDto>> ListarEvaluacionesProveedorAsync(CancellationToken ct = default)
    {
        var sucursal = SucursalActual();
        return await (from evaluacion in db.EvaluacionesProveedor.AsNoTracking()
                      join compra in db.Compras.AsNoTracking() on evaluacion.CompraId equals compra.Id
                      join proveedor in db.Proveedores.AsNoTracking() on evaluacion.ProveedorId equals proveedor.Id
                      where ctx.EsAdmin || compra.SucursalId == sucursal
                      orderby evaluacion.FechaUtc descending
                      select new EvaluacionProveedorDto(evaluacion.CompraId, proveedor.Nombre,
                          evaluacion.Cumplimiento, evaluacion.Puntualidad,
                          (evaluacion.Cumplimiento + evaluacion.Puntualidad) / 2m,
                          evaluacion.Comentarios, evaluacion.FechaUtc))
            .Take(500).ToListAsync(ct);
    }

    public async Task EvaluarProveedorAsync(EvaluacionProveedorRequest req, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Almacen);
        var sucursal = SucursalActual();
        if (req.Cumplimiento is < 1 or > 5 || req.Puntualidad is < 1 or > 5)
            throw new InvalidOperationException("Las evaluaciones deben estar entre 1 y 5.");
        var compra = await db.Compras.FirstOrDefaultAsync(x => x.Id == req.CompraId, ct)
            ?? throw new InvalidOperationException("La compra no existe.");
        if (!ctx.EsAdmin && compra.SucursalId != sucursal) throw new UnauthorizedAccessException("La compra pertenece a otra sucursal.");
        if (compra.Estado != EstadoCompra.Recibida) throw new InvalidOperationException("La evaluación se registra cuando la compra está totalmente recibida.");
        if ((req.Comentarios?.Length ?? 0) > 1000) throw new InvalidOperationException("Los comentarios no pueden exceder 1,000 caracteres.");
        if (await db.EvaluacionesProveedor.AnyAsync(x => x.CompraId == req.CompraId, ct))
            throw new InvalidOperationException("Esta recepción ya cuenta con una evaluación.");
        db.EvaluacionesProveedor.Add(new EvaluacionProveedor
        {
            CompraId = compra.Id, ProveedorId = compra.ProveedorId, UsuarioId = ctx.UsuarioId,
            Cumplimiento = req.Cumplimiento, Puntualidad = req.Puntualidad,
            Comentarios = string.IsNullOrWhiteSpace(req.Comentarios) ? null : req.Comentarios.Trim(),
            FechaUtc = DateTime.UtcNow
        });
        db.AuditLogs.Add(Auditar("ProveedorEvaluado",
            $"Compra {compra.FolioInterno}; cumplimiento {req.Cumplimiento}/5, puntualidad {req.Puntualidad}/5."));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<(int Id, string Folio, string Cliente)>> OrdenesParaEntregaAsync(CancellationToken ct = default)
    {
        var sucursal = SucursalActual();
        return await db.OrdenesVenta.AsNoTracking().Where(x => (ctx.EsAdmin || x.SucursalId == sucursal)
                && x.Estado == EstadoOrdenVenta.Facturada && x.FacturaId.HasValue
                && db.Facturas.Any(f => f.Id == x.FacturaId.Value
                    && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion))
            .Where(x => !db.EntregasReparto.Any(e => e.OrdenVentaId == x.Id && e.Estado != "Cancelada"))
            .OrderByDescending(x => x.FechaEmision)
            .Select(x => new ValueTuple<int, string, string>(x.Id, x.FolioInterno ?? x.Id.ToString(), x.Cliente!.Nombre))
            .Take(200).ToListAsync(ct);
    }

    public async Task<int> ProgramarEntregaAsync(EntregaCrearRequest req, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Vendedor, Roles.Almacen);
        var sucursal = SucursalActual();
        var ruta = req.Ruta?.Trim() ?? string.Empty;
        var chofer = req.Chofer?.Trim() ?? string.Empty;
        var vehiculo = req.Vehiculo?.Trim() ?? string.Empty;
        var fechaProgramadaUtc = req.FechaProgramada.Kind == DateTimeKind.Utc
            ? req.FechaProgramada : req.FechaProgramada.ToUniversalTime();
        if (ruta.Length is < 1 or > 150 || chofer.Length is < 1 or > 150
            || vehiculo.Length is < 1 or > 150 || (req.Placas?.Length ?? 0) > 20
            || fechaProgramadaUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("Ruta, chofer y vehículo son obligatorios (máximo 150 caracteres); la fecha debe ser futura y las placas no exceder 20 caracteres.");
        var orden = await db.OrdenesVenta.Include(x => x.Detalles).Include(x => x.Cliente)
            .FirstOrDefaultAsync(x => x.Id == req.OrdenVentaId, ct)
            ?? throw new InvalidOperationException("La orden no existe.");
        if (!ctx.EsAdmin && orden.SucursalId != sucursal) throw new UnauthorizedAccessException("La orden pertenece a otra sucursal.");
        if (orden.Estado != EstadoOrdenVenta.Facturada)
            throw new InvalidOperationException("Solo se programan órdenes facturadas; las devoluciones deben regularizarse con una nota de crédito.");
        if (!orden.FacturaId.HasValue || !await db.Facturas.AnyAsync(f => f.Id == orden.FacturaId.Value
                && f.Estado != EstadoFactura.Cancelada && f.Estado != EstadoFactura.Devolucion, ct))
            throw new InvalidOperationException("La factura vinculada está cancelada, devuelta o no existe; no se puede programar su reparto.");
        if (await db.EntregasReparto.AnyAsync(x => x.OrdenVentaId == orden.Id && x.Estado != "Cancelada", ct))
            throw new InvalidOperationException("La orden ya tiene un reparto activo.");
        var entrega = new EntregaReparto
        {
            SucursalId = orden.SucursalId, OrdenVentaId = orden.Id, UsuarioId = ctx.UsuarioId,
            Ruta = ruta, Chofer = chofer, Vehiculo = vehiculo,
            Placas = string.IsNullOrWhiteSpace(req.Placas) ? null : req.Placas.Trim(),
            FechaProgramada = fechaProgramadaUtc,
            Detalles = orden.Detalles.Select(d => new EntregaRepartoDetalle
            {
                ProductoId = d.ProductoId, Producto = d.Producto?.Nombre ?? $"Producto #{d.ProductoId}",
                CantidadProgramada = d.Cantidad
            }).ToList()
        };
        db.EntregasReparto.Add(entrega);
        db.AuditLogs.Add(Auditar("RepartoProgramado",
            $"Orden {orden.FolioInterno ?? orden.Id.ToString()}; ruta {entrega.Ruta}, chofer {entrega.Chofer}, vehículo {entrega.Vehiculo}."));
        await db.SaveChangesAsync(ct);
        return entrega.Id;
    }

    public async Task<List<EntregaRepartoDto>> ListarEntregasAsync(CancellationToken ct = default)
    {
        var sucursal = SucursalActual();
        var rows = await db.EntregasReparto.AsNoTracking()
            .Where(x => ctx.EsAdmin || x.SucursalId == sucursal)
            .Include(x => x.Detalles).Include(x => x.CartaPorte)
            .OrderByDescending(x => x.FechaProgramada).Take(300).ToListAsync(ct);
        var orderIds = rows.Select(x => x.OrdenVentaId).Distinct().ToArray();
        var orders = await db.OrdenesVenta.AsNoTracking().Where(x => orderIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FolioInterno, Cliente = x.Cliente!.Nombre })
            .ToDictionaryAsync(x => x.Id, ct);
        return rows.Where(x => orders.ContainsKey(x.OrdenVentaId)).Select(e =>
        {
            var order = orders[e.OrdenVentaId];
            return new EntregaRepartoDto(e.Id, order.Id,
            order.FolioInterno ?? order.Id.ToString(), order.Cliente, e.Ruta, e.Chofer,
            e.Vehiculo, e.Placas, e.Estado, e.FechaProgramada,
            e.Detalles.Select(d => new EntregaDetalleDto(d.ProductoId, d.Producto,
                d.CantidadProgramada, d.CantidadEntregada, d.CantidadDevuelta)).ToList(),
            e.CartaPorte is not null);
        }).ToList();
    }

    public async Task LiquidarEntregaAsync(EntregaLiquidarRequest req, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Vendedor, Roles.Almacen);
        var sucursal = SucursalActual();
        var entrega = await db.EntregasReparto.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == req.EntregaId, ct)
            ?? throw new InvalidOperationException("El reparto no existe.");
        if (!ctx.EsAdmin && entrega.SucursalId != sucursal) throw new UnauthorizedAccessException("El reparto pertenece a otra sucursal.");
        if (entrega.Estado == "Liquidada") throw new InvalidOperationException("El reparto ya fue liquidado.");
        if (req.Renglones.Count != entrega.Detalles.Count) throw new InvalidOperationException("Se debe liquidar cada renglón del reparto.");
        foreach (var dato in req.Renglones)
        {
            var d = entrega.Detalles.SingleOrDefault(x => x.ProductoId == dato.ProductoId)
                ?? throw new InvalidOperationException("El producto no pertenece al reparto.");
            if (dato.Entregada < 0 || dato.Devuelta < 0 || dato.Entregada + dato.Devuelta > d.CantidadProgramada)
                throw new InvalidOperationException($"Las cantidades de {d.Producto} exceden lo programado.");
            if (dato.Entregada + dato.Devuelta != d.CantidadProgramada)
                throw new InvalidOperationException($"Se debe declarar como entregada o devuelta toda la cantidad de {d.Producto}.");
            d.CantidadEntregada = dato.Entregada;
            d.CantidadDevuelta = dato.Devuelta;
        }
        entrega.Estado = entrega.Detalles.All(x => x.CantidadEntregada == x.CantidadProgramada && x.CantidadDevuelta == 0)
            ? "Entregada" : "Liquidada";
        entrega.FechaLiquidacion = DateTime.UtcNow;
        db.AuditLogs.Add(Auditar("RepartoLiquidado",
            $"Entrega #{entrega.Id}; estado {entrega.Estado}; devolución registrada: {entrega.Detalles.Sum(x => x.CantidadDevuelta):N2}."));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ControlTamborDto>> ListarControlesTamborAsync(CancellationToken ct = default)
    {
        var sucursal = SucursalActual();
        return await db.ControlesTambores.AsNoTracking().Where(x => ctx.EsAdmin || x.SucursalId == sucursal)
            .Join(db.Productos.AsNoTracking(), x => x.ProductoId, p => p.Id,
                (x, p) => new ControlTamborDto(x.Id, x.ProductoId, p.Nombre, x.LitrosPorTambor, x.TamboresCerrados, x.LitrosAbiertos))
            .OrderBy(x => x.Producto).ToListAsync(ct);
    }

    public async Task GuardarControlTamborAsync(int id, int productoId, decimal litrosPorTambor, int cerrados, decimal abiertos, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Almacen);
        var sucursal = SucursalActual();
        if (litrosPorTambor <= 0 || cerrados < 0 || abiertos < 0 || abiertos > litrosPorTambor)
            throw new InvalidOperationException("Litros por tambor debe ser positivo; cerrados no negativos y litros abiertos no pueden superar la capacidad.");
        if (!await db.Productos.AnyAsync(x => x.Id == productoId && x.Activo, ct))
            throw new InvalidOperationException("El producto no existe o está inactivo.");
        var litrosDisponibles = await db.InventarioSucursal.AsNoTracking()
            .Where(x => x.SucursalId == sucursal && x.ProductoId == productoId)
            .SumAsync(x => (decimal?)x.StockActual, ct) ?? 0m;
        if (cerrados * litrosPorTambor + abiertos > litrosDisponibles + 0.0001m)
            throw new InvalidOperationException($"La clasificación física excede las existencias del ERP ({litrosDisponibles:N2} L).");
        var control = id == 0
            ? await db.ControlesTambores.FirstOrDefaultAsync(x => x.SucursalId == sucursal && x.ProductoId == productoId, ct) ?? new ControlTambor()
            : await db.ControlesTambores.FirstOrDefaultAsync(x => x.Id == id && (ctx.EsAdmin || x.SucursalId == sucursal), ct)
                ?? throw new InvalidOperationException("El control de tambor no existe.");
        control.SucursalId = sucursal; control.ProductoId = productoId;
        control.LitrosPorTambor = litrosPorTambor; control.TamboresCerrados = cerrados;
        control.LitrosAbiertos = abiertos; control.ActualizadoUtc = DateTime.UtcNow;
        if (control.Id == 0) db.ControlesTambores.Add(control);
        db.AuditLogs.Add(Auditar("ControlTamborActualizado",
            $"Producto {productoId}: {cerrados} cerrados × {litrosPorTambor:N2} L y {abiertos:N2} L abiertos."));
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<EnvaseSaldoDto>> ListarSaldosEnvasesAsync(CancellationToken ct = default)
    {
        var sucursal = SucursalActual();
        var movements = await db.MovimientosEnvaseRetornable.AsNoTracking()
            .Where(x => ctx.EsAdmin || x.SucursalId == sucursal)
            .GroupJoin(db.Clientes.AsNoTracking(), x => x.ClienteId, c => c.Id,
                (x, clientes) => new { x, cliente = clientes.Select(c => c.Nombre).FirstOrDefault() })
            .ToListAsync(ct);
        return movements.GroupBy(x => new { x.x.ClienteId, Cliente = x.cliente ?? "Cliente", x.x.Envase })
            .Select(g => new EnvaseSaldoDto(g.Key.ClienteId, g.Key.Cliente, g.Key.Envase,
                g.Sum(x => x.x.Tipo == "Entregado" ? x.x.Cantidad : -x.x.Cantidad),
                g.Sum(x => (x.x.Tipo == "Entregado" ? 1m : -1m) * x.x.Cantidad * x.x.DepositoUnitario)))
            .Where(x => x.Saldo > 0).OrderBy(x => x.Cliente).ThenBy(x => x.Envase).ToList();
    }

    public async Task RegistrarMovimientoEnvaseAsync(EnvaseMovimientoRequest req, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Vendedor);
        var sucursal = SucursalActual();
        var envase = req.Envase?.Trim() ?? string.Empty;
        if (envase.Length is < 2 or > 80 || req.Cantidad <= 0 || req.DepositoUnitario < 0)
            throw new InvalidOperationException("Envase, cantidad y depósito deben ser válidos.");
        if (!await db.Clientes.AnyAsync(x => x.Id == req.ClienteId && x.Activo, ct))
            throw new InvalidOperationException("El cliente no existe o está inactivo.");
        var tipo = req.Devolucion ? "Devuelto" : "Entregado";
        decimal deposito = req.DepositoUnitario;
        if (req.Devolucion)
        {
            var previos = await db.MovimientosEnvaseRetornable.AsNoTracking()
                .Where(x => x.SucursalId == sucursal && x.ClienteId == req.ClienteId && x.Envase == envase).ToListAsync(ct);
            var saldo = previos.Sum(x => x.Tipo == "Entregado" ? x.Cantidad : -x.Cantidad);
            if (req.Cantidad > saldo) throw new InvalidOperationException($"Solo hay {saldo} envase(s) pendientes de devolución.");
            if (saldo > 0)
                deposito = Math.Max(0, previos.Sum(x => (x.Tipo == "Entregado" ? 1m : -1m) * x.Cantidad * x.DepositoUnitario) / saldo);
        }
        db.MovimientosEnvaseRetornable.Add(new MovimientoEnvaseRetornable
        {
            SucursalId = sucursal, ClienteId = req.ClienteId, Envase = envase,
            Cantidad = req.Cantidad, DepositoUnitario = deposito, Tipo = tipo,
            Referencia = string.IsNullOrWhiteSpace(req.Referencia) ? null : req.Referencia.Trim(),
            UsuarioId = ctx.UsuarioId, FechaUtc = DateTime.UtcNow
        });
        db.AuditLogs.Add(Auditar("EnvaseRetornable",
            $"{tipo}: cliente #{req.ClienteId}, {req.Cantidad} × {envase}, depósito ${deposito:N2} c/u; ref. {req.Referencia}."));
        await db.SaveChangesAsync(ct);
    }

    public async Task<CartaPorteDto> GuardarCartaPorteAsync(CartaPorteRequest req, CancellationToken ct = default)
    {
        ExigirRoles(Roles.Admin, Roles.Vendedor, Roles.Almacen);
        var sucursal = SucursalActual();
        var entrega = await db.EntregasReparto.Include(x => x.CartaPorte)
            .FirstOrDefaultAsync(x => x.Id == req.EntregaId, ct)
            ?? throw new InvalidOperationException("El reparto no existe.");
        if (!ctx.EsAdmin && entrega.SucursalId != sucursal) throw new UnauthorizedAccessException("El reparto pertenece a otra sucursal.");
        var rfcOperador = req.RfcOperador?.Trim() ?? string.Empty;
        var cpOrigen = req.CodigoPostalOrigen?.Trim() ?? string.Empty;
        var cpDestino = req.CodigoPostalDestino?.Trim() ?? string.Empty;
        if (rfcOperador.Length is not (12 or 13)
            || !Regex.IsMatch(rfcOperador, "^[A-ZÑ&]{3,4}[0-9]{6}[A-Z0-9]{3}$", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("El RFC del operador debe tener formato de persona física o moral.");
        if (string.IsNullOrWhiteSpace(req.PermisoSct) || string.IsNullOrWhiteSpace(req.NumeroPermisoSct)
            || string.IsNullOrWhiteSpace(req.ConfiguracionVehicular) || string.IsNullOrWhiteSpace(req.Placas)
            || req.PermisoSct.Length > 30 || req.NumeroPermisoSct.Length > 50
            || req.ConfiguracionVehicular.Length > 10 || req.Placas.Length > 20
            || req.PesoBrutoKg <= 0 || !Regex.IsMatch(cpOrigen, "^\\d{5}$")
            || !Regex.IsMatch(cpDestino, "^\\d{5}$"))
            throw new InvalidOperationException("Completa permiso, vehículo, placas, peso bruto positivo y códigos postales de cinco dígitos.");
        var draft = entrega.CartaPorte ?? new BorradorCartaPorte { EntregaRepartoId = entrega.Id };
        draft.RfcOperador = rfcOperador.ToUpperInvariant();
        draft.PermisoSct = req.PermisoSct.Trim(); draft.NumeroPermisoSct = req.NumeroPermisoSct.Trim();
        draft.ConfiguracionVehicular = req.ConfiguracionVehicular.Trim().ToUpperInvariant();
        draft.Placas = req.Placas.Trim().ToUpperInvariant(); draft.PesoBrutoKg = req.PesoBrutoKg;
        draft.CodigoPostalOrigen = cpOrigen; draft.CodigoPostalDestino = cpDestino;
        draft.ActualizadoUtc = DateTime.UtcNow;
        draft.XmlBorrador = CrearXmlBorrador(draft, entrega);
        if (draft.Id == 0) db.BorradoresCartaPorte.Add(draft);
        db.AuditLogs.Add(Auditar("CartaPorteBorrador",
            $"Borrador Carta Porte 3.1 actualizado para entrega #{entrega.Id}; no timbrado."));
        await db.SaveChangesAsync(ct);
        return new CartaPorteDto(entrega.Id, draft.RfcOperador, draft.PermisoSct,
            draft.NumeroPermisoSct, draft.ConfiguracionVehicular, draft.Placas, draft.PesoBrutoKg,
            draft.CodigoPostalOrigen, draft.CodigoPostalDestino, draft.XmlBorrador);
    }

    private static string CrearXmlBorrador(BorradorCartaPorte d, EntregaReparto entrega)
    {
        var xml = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement(Cfdi + "Complemento",
                new XElement(Cp + "CartaPorte",
                    new XAttribute(XNamespace.Xmlns + "cartaporte31", Cp),
                    new XAttribute("Version", "3.1"),
                    new XAttribute("IdCCP", Guid.NewGuid().ToString("D").ToUpperInvariant()),
                    new XAttribute("TranspInternac", "No"),
                    new XAttribute("TotalDistRec", "0"),
                    new XElement(Cp + "Ubicaciones",
                        new XElement(Cp + "Ubicacion", new XAttribute("TipoUbicacion", "Origen"),
                            new XAttribute("IDUbicacion", "OR000001"),
                            new XElement(Cp + "Domicilio", new XAttribute("CodigoPostal", d.CodigoPostalOrigen))),
                        new XElement(Cp + "Ubicacion", new XAttribute("TipoUbicacion", "Destino"),
                            new XAttribute("IDUbicacion", "DE000001"),
                            new XAttribute("DistanciaRecorrida", "0"),
                            new XElement(Cp + "Domicilio", new XAttribute("CodigoPostal", d.CodigoPostalDestino)))),
                    new XElement(Cp + "Mercancias",
                        new XAttribute("PesoBrutoTotal", d.PesoBrutoKg.ToString("0.##", CultureInfo.InvariantCulture)),
                        new XAttribute("UnidadPeso", "KGM"),
                        new XAttribute("NumTotalMercancias", Math.Max(1, entrega.Detalles.Count)),
                        entrega.Detalles.Select((x, i) => new XElement(Cp + "Mercancia",
                            new XAttribute("BienesTransp", "01010101"),
                            new XAttribute("Descripcion", x.Producto),
                            new XAttribute("Cantidad", x.CantidadProgramada.ToString("0.####", CultureInfo.InvariantCulture)),
                            new XAttribute("ClaveUnidad", "H87"),
                            new XAttribute("PesoEnKg", (d.PesoBrutoKg / Math.Max(1, entrega.Detalles.Count)).ToString("0.##", CultureInfo.InvariantCulture)))) ,
                        new XElement(Cp + "Autotransporte",
                            new XAttribute("PermSCT", d.PermisoSct),
                            new XAttribute("NumPermisoSCT", d.NumeroPermisoSct),
                            new XElement(Cp + "IdentificacionVehicular",
                                new XAttribute("ConfigVehicular", d.ConfiguracionVehicular),
                                new XAttribute("PlacaVM", d.Placas),
                                new XAttribute("AnioModeloVM", DateTime.UtcNow.Year)),
                            new XElement(Cp + "Seguros", new XAttribute("AseguraRespCivil", "PENDIENTE"),
                                new XAttribute("PolizaRespCivil", "PENDIENTE")))),
                    new XElement(Cp + "FiguraTransporte",
                        new XElement(Cp + "TiposFigura",
                            new XAttribute("TipoFigura", "01"),
                            new XAttribute("RFCFigura", d.RfcOperador))))));
        return xml.ToString(SaveOptions.DisableFormatting);
    }

    private AuditLog Auditar(string tipo, string detalle) => new()
    {
        UsuarioId = ctx.UsuarioId,
        Tipo = tipo,
        Detalle = detalle,
        FechaUtc = DateTime.UtcNow
    };
}

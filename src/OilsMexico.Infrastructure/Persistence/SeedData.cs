using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OilsMexico.Domain.Entities;
using OilsMexico.Infrastructure.Persistence;
using OilsMexico.Infrastructure.Services;

namespace OilsMexico.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task InicializarAsync(ErpDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        // EnsureCreated no altera BDs existentes: parcheamos tablas/columnas nuevas (SEPOMEX).
        await SchemaPatch.AplicarAsync(db);
        var admins = await db.Usuarios.Where(u => u.Rol == "Admin" && u.Activo && u.Correo == null)
            .OrderBy(u => u.Id).ToListAsync();
        foreach (var admin in admins)
        {
            admin.Correo = "admin@oilsmexico.local";
            break;
        }
        if (admins.Count > 0) await db.SaveChangesAsync();

        // Parámetros del sistema en tabla configuracion: solo se insertan si faltan
        // (nunca pisan valores ya capturados por el administrador).
        var clavesConfig = new[]
        {
            (ConfiguracionService.ClaveTimeoutSesion, "5"),
            (ConfiguracionService.ClavePasswordMinLongitud, "20"),
            (ConfiguracionService.ClavePasswordHistorial, "3"),
            (ConfiguracionService.ClavePasswordDuracionDias, "90"),
            (ConfiguracionService.ClaveLoginMaxIntentos, "3"),
            (ConfiguracionService.ClaveLoginMinutosBloqueo, "30"),
            (ConfiguracionService.ClaveLoginBloqueosDefinitivo, "2")
        };
        var clavesExistentes = await db.Configuracion.AsNoTracking().Select(c => c.Clave).ToListAsync();
        foreach (var (clave, valor) in clavesConfig)
            if (!clavesExistentes.Contains(clave))
                db.Configuracion.Add(new Configuracion { Clave = clave, Valor = valor });
        await db.SaveChangesAsync();
        if (db.Sucursales.Any()) return;

        var s1 = new Sucursal
        {
            Nombre = "Matriz CDMX", CodigoSucursal = "CDMX01",
            Direccion = "Av. Insurgentes Sur 123, Col. Nápoles, CP 03810, Benito Juárez, CDMX",
            RfcEmisor = "OLU090101AAA",
            RazonSocial = "OilsMexico Aceites y Lubricantes S.A. de C.V.",
            RegimenFiscal = "601", CodigoPostal = "03810",
            Calle = "Av. Insurgentes Sur", NumeroExterior = "123",
            Colonia = "Nápoles", Municipio = "Benito Juárez",
            Estado = "Ciudad de México", Ciudad = "Ciudad de México", Pais = "México",
            Telefono = "55 5555 0101", Email = "matriz@oilsmexico.mx"
        };
        var s2 = new Sucursal
        {
            Nombre = "Sucursal Monterrey", CodigoSucursal = "MTY01",
            Direccion = "Av. Constitución 456, Col. Centro, CP 64000, Monterrey, Nuevo León",
            RfcEmisor = "OLU090101AAA",
            RazonSocial = "OilsMexico Aceites y Lubricantes S.A. de C.V.",
            RegimenFiscal = "601", CodigoPostal = "64000",
            Calle = "Av. Constitución", NumeroExterior = "456",
            Colonia = "Centro", Municipio = "Monterrey",
            Estado = "Nuevo León", Ciudad = "Monterrey", Pais = "México",
            Telefono = "81 8181 0101", Email = "monterrey@oilsmexico.mx"
        };
        db.Sucursales.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var productos = new List<Producto>
        {
            new() { Sku = "MOT-5W30-SYN-1L", Nombre = "Aceite Sintético 5W-30 1L", Marca = "Mobil", Viscosidad = "5W-30", TipoBase = "Sintetico", PrecioVenta = 189, PrecioMayoreo = 165 },
            new() { Sku = "MOT-15W40-MIN-19L", Nombre = "Aceite Mineral 15W-40 Garrafa 19L", Marca = "Castrol", Viscosidad = "15W-40", TipoBase = "Mineral", PrecioVenta = 1450, PrecioMayoreo = 1290 },
            new() { Sku = "MOT-10W30-SEMI-208", Nombre = "Aceite Semisintético 10W-30 Tambor 208L", Marca = "Valvoline", Viscosidad = "10W-30", TipoBase = "Semisintetico", PrecioVenta = 12800, PrecioMayoreo = 11500 },
            new() { Sku = "MOT-5W20-SYN-946", Nombre = "Aceite Sintético 5W-20 946ml", Marca = "Mobil", Viscosidad = "5W-20", TipoBase = "Sintetico", PrecioVenta = 175, PrecioMayoreo = 150 },
        };
        db.Productos.AddRange(productos);
        await db.SaveChangesAsync();

        var unidades = new List<UnidadMedida>();
        foreach (var p in productos)
        {
            unidades.Add(new UnidadMedida { ProductoId = p.Id, UnidadNombre = "Litro", FactorConversion = 1m, PrecioUnitario = p.PrecioVenta / 4 });
            unidades.Add(new UnidadMedida { ProductoId = p.Id, UnidadNombre = "Garrafa", FactorConversion = 19m, PrecioUnitario = p.PrecioVenta });
            unidades.Add(new UnidadMedida { ProductoId = p.Id, UnidadNombre = "Tambor", FactorConversion = 208m, PrecioUnitario = p.PrecioVenta * 9 });
        }
        db.UnidadesMedida.AddRange(unidades);

        db.Clientes.AddRange(
            new Cliente { Nombre = "Público en general", Rfc = "XAXX010101000", RegimenFiscal = "616", TipoPrecio = "menudeo", CodigoPostal = "06600" },
            new Cliente { Nombre = "Taller Hernández SA de CV", Rfc = "THE201015AAA", RegimenFiscal = "601", TipoPrecio = "mayoreo", CodigoPostal = "64000" });

        string Pin(string pin) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pin)));
        db.Usuarios.AddRange(
            new Usuario { Nombre = "Admin", Correo = "admin@oilsmexico.local", PinHash = Pin("1234"), PasswordHash = Pin("1234"), Rol = "Admin", SucursalId = s1.Id },
            new Usuario { Nombre = "Vendedor1", Correo = "vendedor@oilsmexico.local", PinHash = Pin("1111"), PasswordHash = Pin("1111"), Rol = "Vendedor", SucursalId = s1.Id },
            new Usuario { Nombre = "Almacen", Correo = "almacen@oilsmexico.local", PinHash = Pin("2222"), PasswordHash = Pin("2222"), Rol = "Almacen", SucursalId = s1.Id },
            new Usuario { Nombre = "Conta", Correo = "conta@oilsmexico.local", PinHash = Pin("3333"), PasswordHash = Pin("3333"), Rol = "Conta", SucursalId = s1.Id });
        await db.SaveChangesAsync();

        var lote = new InventarioLote { ProductoId = productos[0].Id, NumeroLote = "L-2026-001", FechaFabricacion = new DateOnly(2026, 1, 10), FechaCaducidad = new DateOnly(2029, 1, 10), CantidadDisponible = 500 };
        db.Lotes.Add(lote);
        await db.SaveChangesAsync();
        foreach (var s in new[] { s1, s2 })
            foreach (var p in productos)
                db.InventarioSucursal.Add(new InventarioSucursal
                { SucursalId = s.Id, ProductoId = p.Id, LoteId = p == productos[0] ? lote.Id : null, StockActual = 100, StockMinimo = 5 });
        await db.SaveChangesAsync();
    }
}

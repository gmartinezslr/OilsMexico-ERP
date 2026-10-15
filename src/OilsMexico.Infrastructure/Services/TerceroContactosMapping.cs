using OilsMexico.Application.DTOs;
using OilsMexico.Domain.Entities;

namespace OilsMexico.Infrastructure.Services;

internal static class TerceroContactosMapping
{
    public static string? Validar(
        IReadOnlyCollection<ContactoTerceroDto> contactos,
        IReadOnlyCollection<CorreoTerceroDto> correos,
        IReadOnlyCollection<TelefonoTerceroDto> telefonos,
        IReadOnlyCollection<DireccionTerceroDto> direcciones,
        bool esCliente)
    {
        var personas = contactos.Where(x => !string.IsNullOrWhiteSpace(x.Nombre)).ToList();
        if (personas.Count == 0 || personas.Count(x => x.EsPrincipal) != 1)
            return "Registra un contacto principal.";
        if (personas.Any(x => string.IsNullOrWhiteSpace(x.Clave))
            || personas.Select(x => x.Clave).Distinct(StringComparer.Ordinal).Count() != personas.Count)
            return "Cada contacto debe tener una clave de referencia única.";
        if (esCliente)
        {
            var respaldo = personas.Where(x => x.EsRespaldo).ToList();
            if (respaldo.Count != 1 || respaldo[0].Clave == personas.Single(x => x.EsPrincipal).Clave)
                return "Registra una persona de respaldo distinta del contacto principal.";
            if (!respaldo[0].PuedeRecibirPedidos && !respaldo[0].PuedeRecibirNotificaciones)
                return "Indica si la persona de respaldo puede recibir pedidos o notificaciones.";
            var clave = respaldo[0].Clave;
            if (!correos.Any(x => !string.IsNullOrWhiteSpace(x.Direccion) && x.ContactoClave == clave)
                && !telefonos.Any(x => !string.IsNullOrWhiteSpace(x.Numero) && x.ContactoClave == clave))
                return "La persona de respaldo debe tener al menos un correo o teléfono asociado.";
        }
        else if (personas.Any(x => string.IsNullOrWhiteSpace(x.Puesto)))
        {
            return "Todos los contactos del proveedor deben tener puesto o cargo.";
        }

        if (correos.Count(x => x.EsPrincipal && !string.IsNullOrWhiteSpace(x.Direccion)) > 1)
            return "Solo puede haber un correo principal.";
        if (telefonos.Count(x => x.EsPrincipal && !string.IsNullOrWhiteSpace(x.Numero)) > 1)
            return "Solo puede haber un teléfono principal.";
        if (personas.Count(x => x.EsRespaldo) > 1)
            return "Solo puede haber una persona de respaldo.";

        var claves = personas.Select(x => x.Clave).ToHashSet(StringComparer.Ordinal);
        if (correos.Any(x => !string.IsNullOrWhiteSpace(x.ContactoClave) && !claves.Contains(x.ContactoClave)))
            return "Hay un correo asociado a un contacto inexistente.";
        if (telefonos.Any(x => !string.IsNullOrWhiteSpace(x.ContactoClave) && !claves.Contains(x.ContactoClave)))
            return "Hay un teléfono asociado a un contacto inexistente.";
        if (direcciones.Any(x => !string.IsNullOrWhiteSpace(x.ContactoRecibeClave) && !claves.Contains(x.ContactoRecibeClave)))
            return "Hay una dirección asociada a un contacto inexistente.";

        var direccionesPredeterminadas = direcciones
            .Where(x => x.EsPredeterminada)
            .GroupBy(x => x.Tipo.Trim(), StringComparer.OrdinalIgnoreCase);
        if (direccionesPredeterminadas.Any(x => x.Count() > 1))
            return "Solo puede haber una dirección predeterminada por tipo de uso.";

        return null;
    }

    public static (List<ContactoTercero> Contactos, List<CorreoTercero> Correos, List<TelefonoTercero> Telefonos, List<DireccionTercero> Direcciones)
        CrearEntidades(
            IEnumerable<ContactoTerceroDto> contactosDto,
            IEnumerable<CorreoTerceroDto> correosDto,
            IEnumerable<TelefonoTerceroDto> telefonosDto,
            IEnumerable<DireccionTerceroDto> direccionesDto,
            int? clienteId,
            int? proveedorId)
    {
        var contactos = contactosDto
            .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
            .Select(x => new ContactoTercero
            {
                ClienteId = clienteId,
                ProveedorId = proveedorId,
                Nombre = x.Nombre.Trim(),
                Puesto = Limpiar(x.Puesto),
                Area = Limpiar(x.Area),
                EsPrincipal = x.EsPrincipal,
                EsRespaldo = x.EsRespaldo,
                PuedeRecibirPedidos = x.PuedeRecibirPedidos,
                PuedeRecibirNotificaciones = x.PuedeRecibirNotificaciones,
                AlcanceAutorizacion = Limpiar(x.AlcanceAutorizacion),
                Horario = Limpiar(x.Horario),
                Notas = Limpiar(x.Notas)
            })
            .ToList();
        var claves = contactosDto
            .Where(x => !string.IsNullOrWhiteSpace(x.Nombre))
            .Zip(contactos)
            .ToDictionary(x => x.First.Clave, x => x.Second, StringComparer.Ordinal);

        var correos = correosDto
            .Where(x => !string.IsNullOrWhiteSpace(x.Direccion))
            .Select(x => new CorreoTercero
            {
                ClienteId = clienteId,
                ProveedorId = proveedorId,
                Tipo = string.IsNullOrWhiteSpace(x.Tipo) ? "Otro" : x.Tipo.Trim(),
                Direccion = x.Direccion.Trim(),
                Contacto = BuscarContacto(claves, x.ContactoClave),
                EsPrincipal = x.EsPrincipal,
                Notas = Limpiar(x.Notas)
            })
            .ToList();
        var telefonos = telefonosDto
            .Where(x => !string.IsNullOrWhiteSpace(x.Numero))
            .Select(x => new TelefonoTercero
            {
                ClienteId = clienteId,
                ProveedorId = proveedorId,
                Tipo = string.IsNullOrWhiteSpace(x.Tipo) ? "Otro" : x.Tipo.Trim(),
                Numero = x.Numero.Trim(),
                Contacto = BuscarContacto(claves, x.ContactoClave),
                TieneWhatsApp = x.TieneWhatsApp,
                EsPrincipal = x.EsPrincipal,
                Horario = Limpiar(x.Horario),
                Notas = Limpiar(x.Notas)
            })
            .ToList();
        var direcciones = direccionesDto
            .Where(TieneDireccion)
            .Select(x => new DireccionTercero
            {
                ClienteId = clienteId,
                ProveedorId = proveedorId,
                Tipo = string.IsNullOrWhiteSpace(x.Tipo) ? "Otro" : x.Tipo.Trim(),
                Calle = Limpiar(x.Calle),
                NumeroExterior = Limpiar(x.NumeroExterior),
                NumeroInterior = Limpiar(x.NumeroInterior),
                Colonia = Limpiar(x.Colonia),
                CodigoPostal = Limpiar(x.CodigoPostal),
                Municipio = Limpiar(x.Municipio),
                Estado = Limpiar(x.Estado),
                Ciudad = Limpiar(x.Ciudad),
                Pais = string.IsNullOrWhiteSpace(x.Pais) ? "México" : x.Pais.Trim(),
                ContactoRecibe = BuscarContacto(claves, x.ContactoRecibeClave),
                EsPredeterminada = x.EsPredeterminada,
                Horario = Limpiar(x.Horario),
                Instrucciones = Limpiar(x.Instrucciones)
            })
            .ToList();

        return (contactos, correos, telefonos, direcciones);
    }

    public static void CargarEnDto(
        ICollection<ContactoTercero> contactos,
        ICollection<CorreoTercero> correos,
        ICollection<TelefonoTercero> telefonos,
        ICollection<DireccionTercero> direcciones,
        ClienteCatalogoDto dto)
    {
        dto.Contactos = contactos.Select(C).ToList();
        dto.Correos = correos.Select(E).ToList();
        dto.Telefonos = telefonos.Select(T).ToList();
        dto.Direcciones = direcciones.Select(D).ToList();
    }

    public static void CargarEnDto(
        ICollection<ContactoTercero> contactos,
        ICollection<CorreoTercero> correos,
        ICollection<TelefonoTercero> telefonos,
        ICollection<DireccionTercero> direcciones,
        ProveedorCatalogoDto dto)
    {
        dto.Contactos = contactos.Select(C).ToList();
        dto.Correos = correos.Select(E).ToList();
        dto.Telefonos = telefonos.Select(T).ToList();
        dto.Direcciones = direcciones.Select(D).ToList();
    }

    private static ContactoTerceroDto C(ContactoTercero x) => new()
    {
        Clave = $"id:{x.Id}", Nombre = x.Nombre, Puesto = x.Puesto, Area = x.Area,
        EsPrincipal = x.EsPrincipal, EsRespaldo = x.EsRespaldo,
        PuedeRecibirPedidos = x.PuedeRecibirPedidos, PuedeRecibirNotificaciones = x.PuedeRecibirNotificaciones,
        AlcanceAutorizacion = x.AlcanceAutorizacion, Horario = x.Horario, Notas = x.Notas
    };
    private static CorreoTerceroDto E(CorreoTercero x) => new()
    {
        Clave = $"id:{x.Id}", Tipo = x.Tipo, Direccion = x.Direccion, ContactoClave = x.ContactoTerceroId is null ? null : $"id:{x.ContactoTerceroId}",
        EsPrincipal = x.EsPrincipal, Notas = x.Notas
    };
    private static TelefonoTerceroDto T(TelefonoTercero x) => new()
    {
        Clave = $"id:{x.Id}", Tipo = x.Tipo, Numero = x.Numero, ContactoClave = x.ContactoTerceroId is null ? null : $"id:{x.ContactoTerceroId}",
        TieneWhatsApp = x.TieneWhatsApp, EsPrincipal = x.EsPrincipal, Horario = x.Horario, Notas = x.Notas
    };
    private static DireccionTerceroDto D(DireccionTercero x) => new()
    {
        Clave = $"id:{x.Id}", Tipo = x.Tipo, Calle = x.Calle, NumeroExterior = x.NumeroExterior, NumeroInterior = x.NumeroInterior,
        Colonia = x.Colonia, CodigoPostal = x.CodigoPostal, Municipio = x.Municipio, Estado = x.Estado,
        Ciudad = x.Ciudad, Pais = x.Pais, ContactoRecibeClave = x.ContactoRecibeId is null ? null : $"id:{x.ContactoRecibeId}",
        EsPredeterminada = x.EsPredeterminada, Horario = x.Horario, Instrucciones = x.Instrucciones
    };

    private static ContactoTercero? BuscarContacto(IReadOnlyDictionary<string, ContactoTercero> contactos, string? clave)
        => !string.IsNullOrWhiteSpace(clave) && contactos.TryGetValue(clave, out var contacto) ? contacto : null;

    private static bool TieneDireccion(DireccionTerceroDto x)
        => !string.IsNullOrWhiteSpace(x.Calle) || !string.IsNullOrWhiteSpace(x.Colonia)
            || !string.IsNullOrWhiteSpace(x.CodigoPostal) || !string.IsNullOrWhiteSpace(x.Municipio);

    private static string? Limpiar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

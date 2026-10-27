using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Perfil
{
    // Validacion y armado del criterio de un usuario (hogar, criterios, lugares y alquiler), igual para
    // el perfil y para las versiones de un analisis. Todo se valida antes de tocar la entidad: si algo
    // falla, no queda nada a medio cambiar.
    public static class CriterioUsuario
    {
        // El asistente deja elegir de 1 a 8 personas
        public const int MinimoPersonas = 1;
        public const int MaximoPersonas = 8;

        private static readonly HashSet<string> PrioridadesValidas = new(StringComparer.Ordinal)
        {
            "baja", "media", "alta",
        };

        // Reemplaza por completo hogar, criterios y lugares de "contexto" con lo pedido
        public static void Aplicar(Contexto contexto, CriterioUsuarioRequest solicitud, IReadOnlyList<Criterio> catalogo)
        {
            var integrantes = ArmarIntegrantes(solicitud.Contexto);
            var criterios = ArmarCriterios(solicitud.Criterios, catalogo);
            var lugares = ArmarLugares(solicitud.Puntos);
            var (operacion, compraMaxima, tipoVivienda) = Vivienda.Validar(
                solicitud.Puntos.Operacion, solicitud.Puntos.CompraMaximaUsd, solicitud.Puntos.TipoVivienda);
            var superficie = Vivienda.ValidarSuperficie(solicitud.Puntos.SuperficieM2);

            contexto.Personas = solicitud.Contexto.Personas;
            contexto.Mascotas = solicitud.Contexto.Mascotas.Distinct(StringComparer.Ordinal).ToArray();
            contexto.Movilidad = solicitud.Contexto.Movilidad.Distinct(StringComparer.Ordinal).ToArray();
            contexto.AlquilerMaximo = solicitud.Puntos.AlquilerMaximo;
            contexto.Operacion = operacion;
            contexto.CompraMaximaUsd = compraMaxima;
            contexto.TipoVivienda = tipoVivienda;
            contexto.SuperficieM2 = superficie;

            Reemplazar(contexto.Integrantes, integrantes);
            Reemplazar(contexto.Criterios, criterios);
            Reemplazar(contexto.Puntos, lugares);
        }

        // Personas (1..8) y rangos etarios por integrante; movilidad y mascotas se guardan verbatim
        private static List<Persona> ArmarIntegrantes(ContextoRequest? solicitud)
        {
            if (solicitud is null)
                throw new BadRequestException("El contexto es obligatorio.");

            if (solicitud.Personas < MinimoPersonas || solicitud.Personas > MaximoPersonas)
                throw new BadRequestException($"La cantidad de personas debe estar entre {MinimoPersonas} y {MaximoPersonas}.");

            if (solicitud.Edades.Count > solicitud.Personas)
                throw new BadRequestException("Hay más edades que personas en el contexto.");

            if (solicitud.PersonNames is null)
                throw new BadRequestException("Los nombres de las personas deben ser una lista.");

            if (solicitud.PersonNames.Count > solicitud.Personas)
                throw new BadRequestException("Hay más nombres que personas en el contexto.");

            if (solicitud.PersonNames.Count > 0 && solicitud.PersonNames.Count != solicitud.Personas)
                throw new BadRequestException("Debe indicarse un nombre para cada persona del contexto.");

            if (solicitud.PersonNames.Any(name => string.IsNullOrWhiteSpace(name)))
                throw new BadRequestException("El nombre de cada persona es obligatorio.");

            if (solicitud.PersonNames.Any(name => name!.Trim().Length > 50))
                throw new BadRequestException("El nombre de una persona no puede superar 50 caracteres.");

            if (solicitud.Edades.Any(e => e is { Length: > 40 }))
                throw new BadRequestException("Un rango etario no puede superar 40 caracteres.");

            return Enumerable.Range(1, solicitud.Personas)
                .Select(orden => new Persona
                {
                    Orden = orden,
                    Nombre = solicitud.PersonNames.Count == 0
                        ? $"Persona {orden}"
                        : solicitud.PersonNames[orden - 1]!.Trim(),
                    Edad = orden <= solicitud.Edades.Count ? solicitud.Edades[orden - 1] : null,
                })
                .ToList();
        }

        // La clave debe estar en el catalogo y activa; el valor debe ser baja|media|alta
        private static List<ContextoCriterio> ArmarCriterios(Dictionary<string, string?>? criterios, IReadOnlyList<Criterio> catalogo)
        {
            if (criterios is null)
                throw new BadRequestException("Los criterios deben ser un objeto.");

            var porId = catalogo.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var filas = new List<ContextoCriterio>(criterios.Count);

            foreach (var (id, prioridad) in criterios)
            {
                if (!porId.TryGetValue(id, out var criterio))
                    throw new BadRequestException($"Criterio desconocido: {id}.");

                if (!criterio.Activo)
                    throw new BadRequestException($"Criterio no disponible: {id}.");

                if (prioridad is null || !PrioridadesValidas.Contains(prioridad))
                    throw new BadRequestException($"Prioridad inválida para el criterio {id}.");

                filas.Add(new ContextoCriterio { CriterioId = id, Prioridad = prioridad });
            }

            return filas;
        }

        // Lugares validados de a uno (nombre obligatorio), en el orden recibido
        private static List<PuntoDeInteres> ArmarLugares(PuntosRequest? solicitud)
        {
            if (solicitud is null)
                throw new BadRequestException("Los puntos son obligatorios.");

            if (solicitud.Lugares.Count > LimitesLugares.ConCuenta)
                throw new BadRequestException(LimitesLugares.MensajeConCuenta);

            if (solicitud.AlquilerMaximo?.Length > 30)
                throw new BadRequestException("El alquiler máximo no puede superar 30 caracteres.");

            var lugares = new List<PuntoDeInteres>(solicitud.Lugares.Count);

            for (var i = 0; i < solicitud.Lugares.Count; i++)
            {
                var lugar = solicitud.Lugares[i];
                var nombre = lugar.Nombre?.Trim() ?? string.Empty;
                if (nombre.Length == 0)
                    throw new BadRequestException($"El nombre del lugar {i + 1} es obligatorio.");

                lugares.Add(new PuntoDeInteres
                {
                    Orden = i + 1,
                    Nombre = nombre,
                    Direccion = lugar.Direccion,
                    Latitude = lugar.Latitude,
                    Longitude = lugar.Longitude,
                    Tipo = lugar.Tipo,
                    QuienVa = lugar.QuienVa,
                    Frecuencia = lugar.Frecuencia,
                    Prioridad = lugar.Prioridad,
                    TiempoMax = lugar.TiempoMax,
                });
            }

            return lugares;
        }

        private static void Reemplazar<T>(ICollection<T> actuales, IEnumerable<T> nuevos)
        {
            actuales.Clear();
            foreach (var item in nuevos)
                actuales.Add(item);
        }

        public static ContextoDto ContextoADto(Contexto contexto) =>
            new(
                contexto.Personas,
                contexto.Integrantes
                    .OrderBy(person => person.Orden)
                    .Select(person => string.IsNullOrWhiteSpace(person.Nombre) ? $"Persona {person.Orden}" : person.Nombre.Trim())
                    .ToList(),
                contexto.Integrantes.OrderBy(p => p.Orden).Select(p => p.Edad).ToList(),
                contexto.Movilidad?.ToList() ?? [],
                contexto.Mascotas?.ToList() ?? []);

        public static Dictionary<string, string> CriteriosADto(Contexto contexto) =>
            contexto.Criterios.ToDictionary(c => c.CriterioId, c => c.Prioridad, StringComparer.Ordinal);

        public static PuntosDto PuntosADto(Contexto contexto) =>
            new(
                contexto.AlquilerMaximo,
                contexto.Puntos
                    .OrderBy(p => p.Orden)
                    .Select(p => new LugarDto(
                        p.Id, p.Nombre, p.Direccion, p.Latitude, p.Longitude,
                        p.Tipo, p.QuienVa, p.Frecuencia, p.Prioridad, p.TiempoMax, p.Orden))
                    .ToList(),
                contexto.Operacion,
                contexto.CompraMaximaUsd,
                contexto.TipoVivienda,
                contexto.SuperficieM2);
    }
}

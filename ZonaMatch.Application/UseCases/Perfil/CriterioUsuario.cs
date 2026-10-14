using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
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

            contexto.Personas = solicitud.Contexto.Personas;
            contexto.Mascotas = solicitud.Contexto.Mascotas.Distinct(StringComparer.Ordinal).ToArray();
            contexto.Movilidad = solicitud.Contexto.Movilidad.Distinct(StringComparer.Ordinal).ToArray();
            contexto.AlquilerMaximo = solicitud.Puntos.AlquilerMaximo;

            Reemplazar(contexto.Integrantes, integrantes);
            Reemplazar(contexto.Criterios, criterios);
            Reemplazar(contexto.Puntos, lugares);
        }

        // Personas (1..8) y rangos etarios por integrante; movilidad y mascotas se guardan verbatim
        private static List<Persona> ArmarIntegrantes(ContextoRequest? solicitud)
        {
            if (solicitud is null)
                throw new DatosInvalidosException("El contexto es obligatorio.");

            if (solicitud.Personas < MinimoPersonas || solicitud.Personas > MaximoPersonas)
                throw new DatosInvalidosException($"La cantidad de personas debe estar entre {MinimoPersonas} y {MaximoPersonas}.");

            if (solicitud.Edades.Count > solicitud.Personas)
                throw new DatosInvalidosException("Hay más edades que personas en el contexto.");

            if (solicitud.Edades.Any(e => e is { Length: > 40 }))
                throw new DatosInvalidosException("Un rango etario no puede superar 40 caracteres.");

            return Enumerable.Range(1, solicitud.Personas)
                .Select(orden => new Persona
                {
                    Orden = orden,
                    Edad = orden <= solicitud.Edades.Count ? solicitud.Edades[orden - 1] : null,
                })
                .ToList();
        }

        // La clave debe estar en el catalogo y activa; el valor debe ser baja|media|alta
        private static List<ContextoCriterio> ArmarCriterios(Dictionary<string, string?>? criterios, IReadOnlyList<Criterio> catalogo)
        {
            if (criterios is null)
                throw new DatosInvalidosException("Los criterios deben ser un objeto.");

            var porId = catalogo.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var filas = new List<ContextoCriterio>(criterios.Count);

            foreach (var (id, prioridad) in criterios)
            {
                if (!porId.TryGetValue(id, out var criterio))
                    throw new DatosInvalidosException($"Criterio desconocido: {id}.");

                if (!criterio.Activo)
                    throw new DatosInvalidosException($"Criterio no disponible: {id}.");

                if (prioridad is null || !PrioridadesValidas.Contains(prioridad))
                    throw new DatosInvalidosException($"Prioridad inválida para el criterio {id}.");

                filas.Add(new ContextoCriterio { CriterioId = id, Prioridad = prioridad });
            }

            return filas;
        }

        // Lugares validados de a uno (nombre obligatorio), en el orden recibido
        private static List<PuntoDeInteres> ArmarLugares(PuntosRequest? solicitud)
        {
            if (solicitud is null)
                throw new DatosInvalidosException("Los puntos son obligatorios.");

            if (solicitud.AlquilerMaximo?.Length > 30)
                throw new DatosInvalidosException("El alquiler máximo no puede superar 30 caracteres.");

            var lugares = new List<PuntoDeInteres>(solicitud.Lugares.Count);

            for (var i = 0; i < solicitud.Lugares.Count; i++)
            {
                var lugar = solicitud.Lugares[i];
                var nombre = lugar.Nombre?.Trim() ?? string.Empty;
                if (nombre.Length == 0)
                    throw new DatosInvalidosException($"El nombre del lugar {i + 1} es obligatorio.");

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
                    .ToList());
    }
}

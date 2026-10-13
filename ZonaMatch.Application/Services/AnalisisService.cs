using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class AnalisisService : IAnalisisService
    {
        // Product rule: a registered user may keep up to 3 saved analyses.
        public const int MaximoPorUsuario = 3;

        // El wizard deja elegir de 1 a 8 personas.
        public const int MinimoPersonas = 1;
        public const int MaximoPersonas = 8;

        private static readonly HashSet<string> PrioridadesValidas = new(StringComparer.Ordinal)
        {
            "baja", "media", "alta",
        };

        private readonly IAnalisisRepository _repository;
        private readonly ICriterioRepository _criterios;
        private readonly TimeProvider _timeProvider;

        public AnalisisService(
            IAnalisisRepository repository,
            ICriterioRepository criterios,
            TimeProvider timeProvider)
        {
            _repository = repository;
            _criterios = criterios;
            _timeProvider = timeProvider;
        }

        public async Task<AnalisisDetalladoDto> CrearAsync(Guid usuarioId, CrearAnalisisRequest request, CancellationToken cancellationToken = default)
        {
            var cuenta = await _repository.ContarPorUsuarioAsync(usuarioId, cancellationToken);
            if (cuenta >= MaximoPorUsuario)
                throw new LimiteAnalisisException();

            var ahora = _timeProvider.GetUtcNow();
            var contexto = new Contexto
            {
                UsuarioId = usuarioId,
                Nombre = request.Nombre.Trim(),
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
            };

            // Toda la validacion ocurre antes de guardar: si algo falla, no se persiste nada.
            AplicarContexto(contexto, request.Contexto);
            await AplicarCriteriosAsync(contexto, request.Criterios, cancellationToken);
            AplicarPuntos(contexto, request.Puntos);

            await _repository.AddAsync(contexto, cancellationToken);
            return ToDto(contexto);
        }

        public async Task<IReadOnlyList<AnalisisDetalladoDto>> ObtenerTodosAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var analisis = await _repository.ObtenerPorUsuarioAsync(usuarioId, cancellationToken);
            return analisis.Select(ToDto).ToList();
        }

        public async Task<AnalisisDetalladoDto> ObtenerPorIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var analisis = await _repository.ObtenerPorIdAsync(id, usuarioId, cancellationToken)
                ?? throw new AnalisisNoEncontradoException();

            return ToDto(analisis);
        }

        public async Task<AnalisisDetalladoDto> ActualizarAsync(Guid id, Guid usuarioId, ActualizarAnalisisRequest request, CancellationToken cancellationToken = default)
        {
            var contexto = await _repository.ObtenerPorIdAsync(id, usuarioId, cancellationToken)
                ?? throw new AnalisisNoEncontradoException();

            if (!string.IsNullOrWhiteSpace(request.Nombre))
                contexto.Nombre = request.Nombre.Trim();

            if (request.Contexto is { } contextoBloque)
                AplicarContexto(contexto, contextoBloque);

            if (request.Criterios is { } criteriosBloque)
                await AplicarCriteriosAsync(contexto, criteriosBloque, cancellationToken);

            if (request.Puntos is { } puntosBloque)
                AplicarPuntos(contexto, puntosBloque);

            await _repository.UpdateAsync(contexto, cancellationToken);
            return ToDto(contexto);
        }

        public async Task EliminarAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var contexto = await _repository.ObtenerPorIdAsync(id, usuarioId, cancellationToken)
                ?? throw new AnalisisNoEncontradoException();

            await _repository.DeleteAsync(contexto, cancellationToken);
        }

        // Contexto: personas (1..8) + rangos etarios por integrante; movilidad y mascotas
        // se guardan verbatim, igual que cualquier otra lista simple. Reemplaza el bloque.
        private static void AplicarContexto(Contexto contexto, ContextoRequest? request)
        {
            if (request is null)
                throw new ArgumentException("El contexto es obligatorio.", nameof(request));

            if (request.Personas < MinimoPersonas || request.Personas > MaximoPersonas)
                throw new ArgumentException($"La cantidad de personas debe estar entre {MinimoPersonas} y {MaximoPersonas}.", nameof(request));

            if (request.Edades.Count > request.Personas)
                throw new ArgumentException("Hay más edades que personas en el contexto.", nameof(request));

            if (request.Edades.Any(e => e is { Length: > 40 }))
                throw new ArgumentException("Un rango etario no puede superar 40 caracteres.", nameof(request));

            contexto.Personas = request.Personas;
            contexto.Mascotas = request.Mascotas.Distinct(StringComparer.Ordinal).ToArray();
            contexto.Movilidad = request.Movilidad.Distinct(StringComparer.Ordinal).ToArray();

            contexto.Integrantes.Clear();
            for (var orden = 1; orden <= request.Personas; orden++)
            {
                contexto.Integrantes.Add(new Persona
                {
                    Orden = orden,
                    Edad = orden <= request.Edades.Count ? request.Edades[orden - 1] : null,
                });
            }
        }

        // Criterios: la unica regla de negocio que ya existia. La clave debe estar en el
        // catalogo y activa; el valor debe ser baja|media|alta. Reemplaza el bloque completo.
        private async Task AplicarCriteriosAsync(Contexto contexto, Dictionary<string, string?>? criterios, CancellationToken cancellationToken)
        {
            if (criterios is null)
                throw new ArgumentException("Los criterios deben ser un objeto.", nameof(criterios));

            var catalogo = (await _criterios.ListarAsync(cancellationToken))
                .ToDictionary(c => c.Id, StringComparer.Ordinal);

            var filas = new List<ContextoCriterio>(criterios.Count);

            foreach (var (id, prioridad) in criterios)
            {
                if (!catalogo.TryGetValue(id, out var criterio))
                    throw new ArgumentException($"Criterio desconocido: {id}.", nameof(criterios));

                if (!criterio.Activo)
                    throw new ArgumentException($"Criterio no disponible: {id}.", nameof(criterios));

                if (prioridad is null || !PrioridadesValidas.Contains(prioridad))
                    throw new ArgumentException($"Prioridad inválida para el criterio {id}.", nameof(criterios));

                filas.Add(new ContextoCriterio { CriterioId = id, Prioridad = prioridad });
            }

            contexto.Criterios.Clear();
            foreach (var fila in filas)
            {
                contexto.Criterios.Add(fila);
            }
        }

        // Puntos: lugares validados de a uno (nombre obligatorio) y alquilerMaximo
        // verbatim. Reemplaza el bloque completo.
        private static void AplicarPuntos(Contexto contexto, PuntosRequest? request)
        {
            if (request is null)
                throw new ArgumentException("Los puntos son obligatorios.", nameof(request));

            if (request.AlquilerMaximo?.Length > 30)
                throw new ArgumentException("El alquiler máximo no puede superar 30 caracteres.", nameof(request));

            var lugares = new List<PuntoDeInteres>(request.Lugares.Count);

            for (var i = 0; i < request.Lugares.Count; i++)
            {
                var lugar = request.Lugares[i];
                var nombre = lugar.Nombre?.Trim() ?? string.Empty;
                if (nombre.Length == 0)
                    throw new ArgumentException($"El nombre del lugar {i + 1} es obligatorio.", nameof(request));

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

            contexto.AlquilerMaximo = request.AlquilerMaximo;
            contexto.Puntos.Clear();
            foreach (var lugar in lugares)
            {
                contexto.Puntos.Add(lugar);
            }
        }

        // Response: se reconstruye desde las filas normalizadas manteniendo la misma
        // forma de JSON que ya consume el frontend (personas/edades/movilidad/mascotas,
        // criterios como objeto, lugares con alquilerMaximo).
        private static AnalisisDetalladoDto ToDto(Contexto contexto)
        {
            return new AnalisisDetalladoDto(
                contexto.Id,
                contexto.Nombre,
                new ContextoDto(
                    contexto.Personas,
                    contexto.Integrantes
                        .OrderBy(p => p.Orden)
                        .Select(p => p.Edad)
                        .ToList(),
                    contexto.Movilidad?.ToList() ?? [],
                    contexto.Mascotas?.ToList() ?? []),
                contexto.Criterios
                    .ToDictionary(c => c.CriterioId, c => c.Prioridad, StringComparer.Ordinal),
                new PuntosDto(
                    contexto.AlquilerMaximo,
                    contexto.Puntos
                        .OrderBy(p => p.Orden)
                        .Select(ToDto)
                        .ToList()),
                contexto.FechaCreacion,
                contexto.FechaActualizacion);
        }

        private static LugarDto ToDto(PuntoDeInteres punto) =>
            new(
                punto.Id,
                punto.Nombre,
                punto.Direccion,
                punto.Latitude,
                punto.Longitude,
                punto.Tipo,
                punto.QuienVa,
                punto.Frecuencia,
                punto.Prioridad,
                punto.TiempoMax,
                punto.Orden);
    }
}

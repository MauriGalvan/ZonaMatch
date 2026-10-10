using System.Text.Json;
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

        private static readonly HashSet<string> CriteriosValidos = new(StringComparer.Ordinal)
        {
            "seguridad", "movilidad", "espacios-verdes", "educacion", "salud",
        };

        private static readonly HashSet<string> PrioridadesValidas = new(StringComparer.Ordinal)
        {
            "baja", "media", "alta",
        };

        private readonly IAnalisisRepository _repository;
        private readonly TimeProvider _timeProvider;

        public AnalisisService(IAnalisisRepository repository, TimeProvider timeProvider)
        {
            _repository = repository;
            _timeProvider = timeProvider;
        }

        public async Task<AnalisisDetalladoDto> CrearAsync(Guid usuarioId, CrearAnalisisRequest request, CancellationToken cancellationToken = default)
        {
            var cuenta = await _repository.ContarPorUsuarioAsync(usuarioId, cancellationToken);
            if (cuenta >= MaximoPorUsuario)
                throw new LimiteAnalisisException();

            ValidarCriterios(request.Criterios);

            var ahora = _timeProvider.GetUtcNow();
            var analisis = new AnalisisDetallado
            {
                UsuarioId = usuarioId,
                Nombre = request.Nombre.Trim(),
                ContextoJson = request.Contexto.GetRawText(),
                CriteriosJson = request.Criterios.GetRawText(),
                PuntosJson = request.Puntos.GetRawText(),
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
            };

            await _repository.AddAsync(analisis, cancellationToken);
            return ToDto(analisis);
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
            var analisis = await _repository.ObtenerPorIdAsync(id, usuarioId, cancellationToken)
                ?? throw new AnalisisNoEncontradoException();

            if (!string.IsNullOrWhiteSpace(request.Nombre))
                analisis.Nombre = request.Nombre.Trim();

            if (request.Contexto is { } contexto)
            {
                analisis.ContextoJson = contexto.GetRawText();
            }

            if (request.Criterios is { } criterios)
            {
                ValidarCriterios(criterios);
                analisis.CriteriosJson = criterios.GetRawText();
            }

            if (request.Puntos is { } puntos)
            {
                analisis.PuntosJson = puntos.GetRawText();
            }

            await _repository.UpdateAsync(analisis, cancellationToken);
            return ToDto(analisis);
        }

        public async Task EliminarAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var analisis = await _repository.ObtenerPorIdAsync(id, usuarioId, cancellationToken)
                ?? throw new AnalisisNoEncontradoException();

            await _repository.DeleteAsync(analisis, cancellationToken);
        }

        // Criterios are the only part of the payload the server checks: known ids, known priorities.
        // Contexto and puntos keep evolving with the frontend, so they are stored verbatim.
        private static void ValidarCriterios(JsonElement criterios)
        {
            if (criterios.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Los criterios deben ser un objeto.", nameof(criterios));

            foreach (var property in criterios.EnumerateObject())
            {
                if (!CriteriosValidos.Contains(property.Name))
                    throw new ArgumentException($"Criterio desconocido: {property.Name}.", nameof(criterios));

                var prioridad = property.Value.GetString();
                if (prioridad is null || !PrioridadesValidas.Contains(prioridad))
                    throw new ArgumentException($"Prioridad inválida para el criterio {property.Name}.", nameof(criterios));
            }
        }

        private static AnalisisDetalladoDto ToDto(AnalisisDetallado analisis)
        {
            return new AnalisisDetalladoDto(
                analisis.Id,
                analisis.Nombre,
                JsonDocument.Parse(analisis.ContextoJson).RootElement,
                JsonDocument.Parse(analisis.CriteriosJson).RootElement,
                JsonDocument.Parse(analisis.PuntosJson).RootElement,
                analisis.FechaCreacion,
                analisis.FechaActualizacion);
        }
    }
}

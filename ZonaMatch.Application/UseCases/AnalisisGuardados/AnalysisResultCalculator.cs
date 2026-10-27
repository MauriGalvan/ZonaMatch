using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.AnalisisGuardados
{
    // Result of a saved analysis, computed once when it is saved or recalculated, so opening it later shows what the
    // user saw and two versions can be compared. Uses the same scoring as the analysis screen (POST /Puntajes) with the
    // analysis's own criteria: its saved zones are always included and the recommendations are the next in the ranking.
    public class AnalysisResultCalculator(CalcularPuntajes scoring) : IAnalysisResultCalculator
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<PuntajesDto?> CalculateAsync(Analisis analysis, CancellationToken cancellationToken = default)
        {
            var zones = analysis.Zonas.OrderBy(zone => zone.Orden).ToList();
            if (zones.Count == 0)
                return null;

            var context = analysis.Contexto;
            var request = new CalcularPuntajesRequest
            {
                ZonaInicial = zones.FirstOrDefault(zone => zone.EsInicial)?.ZonaSlug,
                ZonasAgregadas = zones.Where(zone => !zone.EsInicial).Select(zone => zone.ZonaSlug).ToList(),
                // Trips from the starting points the user marked, as when the analysis was shown
                OrigenesZona = zones
                    .Where(zone => zone.TieneOrigen)
                    .Select(zone => new OrigenZonaRequest
                    {
                        Slug = zone.ZonaSlug,
                        Latitude = zone.OrigenLatitud!.Value,
                        Longitude = zone.OrigenLongitud!.Value,
                    })
                    .ToList(),
                Criterios = context.Criterios.ToDictionary(c => c.CriterioId, c => (string?)c.Prioridad),
                Lugares = context.Puntos
                    .OrderBy(point => point.Orden)
                    .Select(point => new LugarPuntajeRequest
                    {
                        Nombre = point.Nombre,
                        Latitude = point.Latitude,
                        Longitude = point.Longitude,
                        Prioridad = point.Prioridad,
                        TiempoMax = point.TiempoMax,
                    })
                    .ToList(),
                AlquilerMaximo = context.AlquilerMaximo,
                Operacion = context.Operacion,
                CompraMaximaUsd = context.CompraMaximaUsd,
                TipoVivienda = context.TipoVivienda,
                Movilidad = (context.Movilidad ?? []).ToList(),
            };

            PuntajesDto result;
            try
            {
                result = await scoring.EjecutarAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is NotFoundException or BadRequestException)
            {
                // A zone without data does not block saving the analysis: it is shown with the live calculation
                return null;
            }

            // The saved zones in score order (the automatic top may bring others), and the rest as recommendations
            var saved = zones.Select(zone => zone.ZonaSlug).ToHashSet(StringComparer.Ordinal);
            return result with
            {
                Zonas = result.Zonas.Where(zone => saved.Contains(zone.Slug)).ToList(),
                Cercanas = result.Zonas.Concat(result.Cercanas).Where(zone => !saved.Contains(zone.Slug)).Take(CalcularPuntajes.RecomendadasDevueltas).ToList(),
            };
        }

        public static string Serialize(PuntajesDto result) => JsonSerializer.Serialize(result, JsonOptions);

        public static PuntajesDto? Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<PuntajesDto>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}

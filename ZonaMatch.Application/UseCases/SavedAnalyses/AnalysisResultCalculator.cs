using System.Text.Json;
using System.Text.Json.Nodes;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Result of a saved analysis, computed once when it is saved or recalculated, so opening it later shows what the
    // user saw and two versions can be compared. Uses the same scoring as the analysis screen (POST /api/v1/scores) with the
    // analysis's own criteria: its saved zones are always included and the recommendations are the next in the ranking.
    public class AnalysisResultCalculator(CalculateScores scoring) : IAnalysisResultCalculator
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly IReadOnlyDictionary<string, string> LegacyScorePropertyNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["modoViaje"] = "travelMode",
            ["zonas"] = "zones",
            ["cercanas"] = "recommendations",
            ["nombre"] = "name",
            ["puntaje"] = "score",
            ["componentes"] = "components",
            ["limites"] = "limits",
            ["lugares"] = "places",
            ["alquiler"] = "rent",
            ["compra"] = "sale",
            ["criterio"] = "criterion",
            ["prioridad"] = "priority",
            ["detalle"] = "details",
            ["tipo"] = "type",
            ["cumple"] = "isMet",
            ["minutos"] = "minutes",
            ["maximoMinutos"] = "maximumMinutes",
            ["distanciaKm"] = "distanceKm",
            ["promedio"] = "average",
            ["propiedadesDisponibles"] = "availableProperties",
            ["expensas"] = "expenses",
            ["costoMensual"] = "monthlyCost",
            ["precioM2"] = "pricePerM2",
            ["superficieM2"] = "surfaceM2",
            ["promedioUsd"] = "averageUsd",
            ["precioM2Usd"] = "pricePerM2Usd",
            ["estimadoUsd"] = "estimatedUsd",
        };

        public async Task<ScoresResponse?> CalculateAsync(Analisis analysis, CancellationToken cancellationToken = default)
        {
            var zones = analysis.Zonas.OrderBy(zone => zone.Orden).ToList();
            if (zones.Count == 0)
                return null;

            var context = analysis.Contexto;
            var request = new CalculateScoresRequest
            {
                InitialZone = zones.FirstOrDefault(zone => zone.EsInicial)?.ZonaSlug,
                AddedZones = zones.Where(zone => !zone.EsInicial).Select(zone => zone.ZonaSlug).ToList(),
                // Trips from the starting points the user marked, as when the analysis was shown
                ZoneOrigins = zones
                    .Where(zone => zone.TieneOrigen)
                    .Select(zone => new ZoneOriginRequest
                    {
                        Slug = zone.ZonaSlug,
                        Latitude = zone.OrigenLatitud!.Value,
                        Longitude = zone.OrigenLongitud!.Value,
                    })
                    .ToList(),
                Criteria = context.Criterios.ToDictionary(c => c.CriterioId, c => (string?)c.Prioridad),
                Places = context.Puntos
                    .OrderBy(point => point.Orden)
                    .Select(point => new PlaceScoreRequest
                    {
                        Name = point.Nombre,
                        Latitude = point.Latitude,
                        Longitude = point.Longitude,
                        Priority = point.Prioridad,
                        MaximumTravelTime = point.TiempoMax,
                    })
                    .ToList(),
                MaximumRent = context.AlquilerMaximo,
                Operation = context.Operacion,
                MaximumPurchasePriceUsd = context.CompraMaximaUsd,
                HousingType = context.TipoVivienda,
                AreaM2 = context.SuperficieM2,
                Mobility = (context.Movilidad ?? []).ToList(),
            };

            ScoresResponse result;
            try
            {
                result = await scoring.ExecuteAsync(request, cancellationToken);
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
                Zones = result.Zones.Where(zone => saved.Contains(zone.Slug)).ToList(),
                Recommendations = result.Zones.Concat(result.Recommendations)
                    .Where(zone => !saved.Contains(zone.Slug))
                    .Take(CalculateScores.RecommendationsReturned)
                    .ToList(),
            };
        }

        public static string Serialize(ScoresResponse result) => JsonSerializer.Serialize(result, JsonOptions);

        public static ScoresResponse? Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var node = JsonNode.Parse(json);
                NormalizeLegacyScorePropertyNames(node);
                return node?.Deserialize<ScoresResponse>(JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void NormalizeLegacyScorePropertyNames(JsonNode? node, string? parentName = null)
        {
            if (node is JsonObject jsonObject)
            {
                foreach (var (name, value) in jsonObject.ToArray())
                {
                    NormalizeLegacyScorePropertyNames(value, name);
                    var currentName = name == "estimado" && parentName == "alquiler"
                        ? "estimated"
                        : LegacyScorePropertyNames.GetValueOrDefault(name);
                    if (currentName is null)
                        continue;

                    jsonObject.Remove(name);
                    if (!jsonObject.ContainsKey(currentName))
                        jsonObject[currentName] = value;
                }
            }
            else if (node is JsonArray jsonArray)
            {
                foreach (var item in jsonArray)
                    NormalizeLegacyScorePropertyNames(item, parentName);
            }
        }
    }
}

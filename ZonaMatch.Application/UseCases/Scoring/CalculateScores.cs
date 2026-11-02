using System.Text.RegularExpressions;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.Scoring
{
    // Ranks zones containing the user's places and their contiguous neighbors.
    public partial class CalculateScores
    {
        public const int AutomaticTopZones = 5;

        // Maximum travel time when a place does not specify one.
        public const int DefaultMaximumTravelTime = 45;

        public const int RecommendationsReturned = 4;

        // Number of routing requests sent concurrently.
        private const int ConcurrentTrips = 6;

        private readonly IPerfilesZonaRepository _zoneProfiles;
        private readonly IRuteoClient _routing;
        private readonly IZonaRepository _zones;

        public CalculateScores(IPerfilesZonaRepository zoneProfiles, IRuteoClient routing, IZonaRepository zones)
        {
            _zoneProfiles = zoneProfiles;
            _routing = routing;
            _zones = zones;
        }

        public async Task<ScoresResponse> ExecuteAsync(CalculateScoresRequest request, CancellationToken cancellationToken = default)
        {
            var zoneUniverse = await _zoneProfiles.ListarAsync(cancellationToken);
            var zonesBySlug = zoneUniverse.ToDictionary(zone => zone.Slug);
            if (zoneUniverse.Count == 0)
                throw new NotFoundException("Todavía no hay zonas disponibles para calcular el ranking.");

            var scoringRequest = BuildRequest(request);
            var travelMode = TravelModes.FromMobility(request.Mobility);
            var scoringEngine = new ScoringEngine(zoneUniverse);
            var addedZones = ResolveAddedZones(request.AddedZones, zonesBySlug);
            var initialZoneSlug = request.InitialZone?.Trim();
            var initialZone = string.IsNullOrWhiteSpace(initialZoneSlug)
                ? null
                : zonesBySlug.TryGetValue(initialZoneSlug, out var foundZone)
                    ? foundZone
                    : throw new NotFoundException("No hay datos para la zona inicial indicada.");

            var locations = scoringRequest.Lugares
                .Where(place => place.TieneUbicacion)
                .Select(place => (Latitud: place.Latitud!.Value, Longitud: place.Longitud!.Value))
                .ToArray();
            var surroundingZoneSlugs = locations.Length == 0
                ? zoneUniverse.Select(zone => zone.Slug).ToArray()
                : await _zoneProfiles.ListarZonasDelEntornoAsync(locations, cancellationToken);
            var surroundingZones = surroundingZoneSlugs
                .Where(zonesBySlug.ContainsKey)
                .Select(slug => zonesBySlug[slug])
                .ToArray();
            if (locations.Length > 0 && surroundingZones.Length == 0)
                throw new BadRequestException("No se encontraron zonas del catálogo que contengan los puntos de interés indicados.");

            var candidateZones = surroundingZones
                .Concat(addedZones)
                .Concat(initialZone is { } initial ? [initial] : Array.Empty<ZoneProfile>())
                .DistinctBy(zone => zone.Slug, StringComparer.Ordinal)
                .ToArray();

            var origins = await ResolveOriginsAsync(request.ZoneOrigins, zonesBySlug, cancellationToken);
            var travelTimes = await MeasureTripsAsync(candidateZones, scoringRequest.Lugares, travelMode, origins, cancellationToken);
            var ranking = candidateZones
                .Select(zone => scoringEngine.Calcular(zone, scoringRequest, travelTimes[zone.Slug]))
                .OrderByDescending(result => result.Puntaje.HasValue)
                .ThenByDescending(result => result.Puntaje)
                .ThenBy(result => result.Zona.Nombre, StringComparer.CurrentCulture)
                .ToArray();

            var top = ranking.Take(AutomaticTopZones).ToList();
            if (initialZone is not null && top.All(result => result.Zona.Slug != initialZone.Slug))
            {
                if (top.Count == AutomaticTopZones)
                    top.RemoveAt(top.Count - 1);
                top.Add(ranking.Single(result => result.Zona.Slug == initialZone.Slug));
                top = top
                    .OrderByDescending(result => result.Puntaje.HasValue)
                    .ThenByDescending(result => result.Puntaje)
                    .ThenBy(result => result.Zona.Nombre, StringComparer.CurrentCulture)
                    .ToList();
            }

            var includedZones = top
                .Concat(addedZones.Select(zone => ranking.Single(result => result.Zona.Slug == zone.Slug)))
                .DistinctBy(result => result.Zona.Slug, StringComparer.Ordinal)
                .OrderByDescending(result => result.Puntaje.HasValue)
                .ThenByDescending(result => result.Puntaje)
                .ThenBy(result => result.Zona.Nombre, StringComparer.CurrentCulture)
                .ToArray();
            var includedSlugs = includedZones.Select(result => result.Zona.Slug).ToHashSet(StringComparer.Ordinal);
            var recommendations = ranking
                .Where(result => !includedSlugs.Contains(result.Zona.Slug))
                .Take(RecommendationsReturned)
                .ToArray();

            return new ScoresResponse(
                TravelModes.ToCode(travelMode),
                includedZones.Select(result => ToResponse(result, scoringRequest)).ToArray(),
                recommendations.Select(result => ToResponse(result, scoringRequest)).ToArray());
        }

        private static ZoneProfile[] ResolveAddedZones(
            IReadOnlyCollection<string>? requestedSlugs,
            IReadOnlyDictionary<string, ZoneProfile> zonesBySlug)
        {
            var slugs = (requestedSlugs ?? [])
                .Select(slug => slug?.Trim() ?? string.Empty)
                .Where(slug => slug.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var missingSlugs = slugs.Where(slug => !zonesBySlug.ContainsKey(slug)).ToArray();
            if (missingSlugs.Length > 0)
                throw new NotFoundException("No se encontraron una o más zonas agregadas al análisis.");

            return slugs.Select(slug => zonesBySlug[slug]).ToArray();
        }

        // Starting points chosen by the user, by zone slug. Each one must fall inside its zone.
        private async Task<Dictionary<string, (double Latitude, double Longitude)>> ResolveOriginsAsync(
            IReadOnlyCollection<ZoneOriginRequest>? origins, IReadOnlyDictionary<string, ZoneProfile> zonesBySlug,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, (double Latitude, double Longitude)>(StringComparer.Ordinal);
            foreach (var origin in origins ?? [])
            {
                var slug = origin.Slug.Trim();
                // A starting point for a zone that does not exist cannot be measured
                if (!zonesBySlug.ContainsKey(slug))
                    throw new NotFoundException("No se encontró la zona del punto de partida indicado.");

                // The point replaces the center of the zone, so it has to be inside it
                if (!await _zones.ContienePuntoAsync(slug, origin.Latitude, origin.Longitude, cancellationToken))
                    throw new BadRequestException("El punto de partida tiene que estar dentro de su zona.");

                result[slug] = (origin.Latitude, origin.Longitude);
            }

            return result;
        }

        // One trip per zone and located place, starting at the zone center or at the point the user chose in it.
        // Estimate the time if routing is unavailable.
        private async Task<Dictionary<string, IReadOnlyList<TravelTime?>>> MeasureTripsAsync(
            IReadOnlyList<ZoneProfile> zones, IReadOnlyList<UserPlace> places, TravelMode travelMode,
            IReadOnlyDictionary<string, (double Latitude, double Longitude)> origins,
            CancellationToken cancellationToken)
        {
            using var concurrencyLimit = new SemaphoreSlim(ConcurrentTrips);

            async Task<TravelTime?> MeasureAsync(ZoneProfile zone, UserPlace place)
            {
                if (!place.TieneUbicacion)
                    return null;

                var (latitude, longitude) = origins.TryGetValue(zone.Slug, out var origin) ? origin : (zone.Latitud, zone.Longitud);

                await concurrencyLimit.WaitAsync(cancellationToken);
                try
                {
                    // Five blocks or less are walked (TravelModes.WalkingDistanceThresholdKm)
                    var routeMode = TravelModes.ForDistance(travelMode, latitude, longitude, place.Latitud!.Value, place.Longitud!.Value);
                    return await _routing.CalcularAsync(latitude, longitude, place.Latitud!.Value, place.Longitud!.Value, routeMode, cancellationToken)
                           ?? TravelModes.Estimate(routeMode, latitude, longitude, place.Latitud!.Value, place.Longitud!.Value);
                }
                finally
                {
                    concurrencyLimit.Release();
                }
            }

            var byZone = zones
                .DistinctBy(z => z.Slug)
                .Select(async zone => (zone.Slug, Times: await Task.WhenAll(places.Select(place => MeasureAsync(zone, place)))))
                .ToList();

            var results = await Task.WhenAll(byZone);
            return results.ToDictionary(result => result.Slug, result => (IReadOnlyList<TravelTime?>)result.Times);
        }

        private static ScoringRequest BuildRequest(CalculateScoresRequest request)
        {
            var criteria = request.Criteria
                .Where(c => ScoringEngine.EsCriterioConocido(c.Key) && c.Value is "baja" or "media" or "alta")
                .ToDictionary(c => c.Key, c => c.Value!);

            var places = request.Places
                .Select(place => new UserPlace(place.Name.Trim(), place.Latitude, place.Longitude, place.Priority, ParseMinutes(place.MaximumTravelTime)))
                .ToList();

            var (operation, maximumPurchase, housingType) = HousingScoring.Validate(
                request.Operation, request.MaximumPurchasePriceUsd, request.HousingType);

            return new ScoringRequest(
                criteria, places, ParseRent(request.MaximumRent), operation, maximumPurchase, housingType,
                HousingScoring.ValidateSurface(request.AreaM2));
        }

        // "45 min" -> 45.
        private static int ParseMinutes(string? value) =>
            value is not null && Number().Match(value) is { Success: true } match && int.TryParse(match.Value, out var minutes) && minutes > 0
                ? minutes
                : DefaultMaximumTravelTime;

        // "$ 800.000" or "800000" -> 800000; null when no number is present.
        private static decimal? ParseRent(string? amount)
        {
            var digits = new string((amount ?? string.Empty).Where(char.IsDigit).ToArray());
            return decimal.TryParse(digits, out var value) && value > 0 ? value : null;
        }

        [GeneratedRegex(@"\d+")]
        private static partial Regex Number();

        private static ScoredZoneResponse ToResponse(ZoneScoreResult result, ScoringRequest request) =>
            new(
                result.Zona.Slug,
                result.Zona.Nombre,
                result.Puntaje,
                result.Componentes.Select(component => new ScoreComponentResponse(component.Criterion, component.Prioridad, component.Puntaje, component.Detalle)).ToList(),
                result.Limites.Select(limit => new ScoreLimitResponse(limit.Tipo, limit.Cumple, limit.Detalle)).ToList(),
                result.Lugares.Select(place => new PlaceTravelTimeResponse(
                    place.Nombre, place.Minutos, place.MaximoMinutos, place.Estimado, place.DistanciaKm)).ToList(),
                HousingScoring.Rent(result.Zona, request.TipoVivienda, request.SuperficieM2),
                HousingScoring.Sale(result.Zona, request.TipoVivienda, request.SuperficieM2));
    }
}

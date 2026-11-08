using System.Text.RegularExpressions;
using ZonaMatch.Application.DTOs.Scoring.Requests;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
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

        // When no zone around the user's places reaches this score, the ranking also looks at the rest of the catalog.
        public const int MinimumExpectedScore = 30;

        // Zones from the rest of the catalog whose trips are measured: the best ones by estimated trip times.
        public const int ExpandedSearchZones = 10;

        // Number of routing requests sent concurrently.

        private readonly IZoneProfilesRepository _zoneProfiles;
        private readonly IRoutingClient _routing;
        private readonly IZoneRepository _zones;

        public CalculateScores(IZoneProfilesRepository zoneProfiles, IRoutingClient routing, IZoneRepository zones)
        {
            _zoneProfiles = zoneProfiles;
            _routing = routing;
            _zones = zones;
        }

        // A ranking requested by a person: guests score fewer places, and every limit is required.
        public Task<ScoresResponse> ExecuteAsync(CalculateScoresRequest request, bool isGuest, CancellationToken cancellationToken = default)
        {
            // Reject more places than a guest can score.
            if (isGuest && request.Places.Count > ScoringPlaceLimits.Guest)
                throw new BadRequestException(ScoringPlaceLimits.GuestMessage);

            MandatoryScoringLimits.Validate(
                request.MaximumRent, request.Operation, request.MaximumPurchasePriceUsd, request.Places.Select(place => place.MaximumTravelTime));

            return CalculateAsync(request, cancellationToken);
        }

        // Only calculates. Saved analyses recalculate through here, so older ones saved without limits still work.
        public async Task<ScoresResponse> CalculateAsync(CalculateScoresRequest request, CancellationToken cancellationToken = default)
        {
            var zoneUniverse = await _zoneProfiles.ListAsync(cancellationToken);
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

            var locations = scoringRequest.Places
                .Where(place => place.HasLocation)
                .Select(place => (Latitude: place.Latitude!.Value, Longitude: place.Longitude!.Value))
                .ToArray();
            var surroundingZoneSlugs = locations.Length == 0
                ? zoneUniverse.Select(zone => zone.Slug).ToArray()
                : await _zoneProfiles.ListNearbyZoneSlugsAsync(locations, cancellationToken);
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
            using var routeLimit = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);
            var travelTimes = await ZoneTripTimes.MeasureAsync(
                _routing, candidateZones, scoringRequest.Places, travelMode, routeLimit, cancellationToken, origins,
                TravelModes.HasBicycle(request.Mobility));
            var ranking = candidateZones
                .Select(zone => scoringEngine.Calculate(zone, scoringRequest, travelTimes[zone.Slug]))
                .OrderByDescending(result => result.Score.HasValue)
                .ThenByDescending(result => result.Score)
                .ThenBy(result => result.Zone.Name, StringComparer.CurrentCulture)
                .ToArray();

            var searchExpanded = false;
            if (!(ranking.Max(result => result.Score) >= MinimumExpectedScore))
            {
                var expandedResults = await ScoreRestOfCatalogAsync(
                    zoneUniverse, candidateZones, surroundingZones, scoringEngine, scoringRequest, travelMode, routeLimit, cancellationToken);
                var bestInitialScore = ranking.Max(result => result.Score) ?? double.MinValue;
                searchExpanded = expandedResults.Any(result => result.Score > bestInitialScore);
                ranking = ranking
                    .Concat(expandedResults)
                    .OrderByDescending(result => result.Score.HasValue)
                    .ThenByDescending(result => result.Score)
                    .ThenBy(result => result.Zone.Name, StringComparer.CurrentCulture)
                    .ToArray();
            }

            var top = ranking.Take(AutomaticTopZones).ToList();
            if (initialZone is not null && top.All(result => result.Zone.Slug != initialZone.Slug))
            {
                if (top.Count == AutomaticTopZones)
                    top.RemoveAt(top.Count - 1);
                top.Add(ranking.Single(result => result.Zone.Slug == initialZone.Slug));
                top = top
                    .OrderByDescending(result => result.Score.HasValue)
                    .ThenByDescending(result => result.Score)
                    .ThenBy(result => result.Zone.Name, StringComparer.CurrentCulture)
                    .ToList();
            }

            var includedZones = top
                .Concat(addedZones.Select(zone => ranking.Single(result => result.Zone.Slug == zone.Slug)))
                .DistinctBy(result => result.Zone.Slug, StringComparer.Ordinal)
                .OrderByDescending(result => result.Score.HasValue)
                .ThenByDescending(result => result.Score)
                .ThenBy(result => result.Zone.Name, StringComparer.CurrentCulture)
                .ToArray();
            var includedSlugs = includedZones.Select(result => result.Zone.Slug).ToHashSet(StringComparer.Ordinal);
            var recommendations = ranking
                .Where(result => !includedSlugs.Contains(result.Zone.Slug))
                .Take(RecommendationsReturned)
                .ToArray();

            return new ScoresResponse(
                TravelModes.ToCode(travelMode),
                includedZones.Select(result => ToResponse(result, scoringRequest)).ToArray(),
                recommendations.Select(result => ToResponse(result, scoringRequest)).ToArray(),
                searchExpanded);
        }

        // Scores the zones outside the initial search. Routing every zone would take too long, so they are first
        // ranked with estimated trip times and only the best ExpandedSearchZones are measured.
        // Only zones of the same administrative level as the surrounding ones are compared, as the surrounding search
        // does: neighborhoods with neighborhoods, never a neighborhood with the comuna or partido that contains it.
        private async Task<ZoneScoreResult[]> ScoreRestOfCatalogAsync(
            IReadOnlyList<ZoneProfile> zoneUniverse, IReadOnlyCollection<ZoneProfile> candidateZones,
            IReadOnlyCollection<ZoneProfile> surroundingZones, ScoringEngine scoringEngine,
            ScoringRequest scoringRequest, TravelMode travelMode, SemaphoreSlim routeLimit, CancellationToken cancellationToken)
        {
            var candidateSlugs = candidateZones.Select(zone => zone.Slug).ToHashSet(StringComparer.Ordinal);
            var comparableLevels = surroundingZones.Select(zone => zone.Level).ToHashSet();
            var bestByEstimate = zoneUniverse
                .Where(zone => !candidateSlugs.Contains(zone.Slug) && comparableLevels.Contains(zone.Level))
                .Select(zone => scoringEngine.Calculate(zone, scoringRequest, EstimateTrips(zone, scoringRequest.Places, travelMode)))
                .Where(result => result.Score.HasValue && ReachesEveryPlace(result))
                .OrderByDescending(result => result.Score)
                .Take(ExpandedSearchZones)
                .Select(result => result.Zone)
                .ToArray();
            if (bestByEstimate.Length == 0)
                return [];

            var travelTimes = await ZoneTripTimes.MeasureAsync(
                _routing, bestByEstimate, scoringRequest.Places, travelMode, routeLimit, cancellationToken);
            return bestByEstimate
                .Select(zone => scoringEngine.Calculate(zone, scoringRequest, travelTimes[zone.Slug]))
                .Where(ReachesEveryPlace)
                .ToArray();
        }

        // A far zone is only worth suggesting when every trip was measured and fits the place's maximum time: an
        // unknown trip does not lower the score, so a zone with no route would otherwise rank first.
        private static bool ReachesEveryPlace(ZoneScoreResult result) =>
            result.Places.All(place => place.Minutes is { } minutes && minutes <= place.MaximumMinutes);

        private static TravelTime?[] EstimateTrips(ZoneProfile zone, IReadOnlyList<UserPlace> places, TravelMode travelMode) =>
            places
                .Select(place => place.HasLocation
                    ? TravelModes.Estimate(
                        TravelModes.ForDistance(travelMode, zone.Latitude, zone.Longitude, place.Latitude!.Value, place.Longitude!.Value),
                        zone.Latitude, zone.Longitude, place.Latitude!.Value, place.Longitude!.Value)
                    : (TravelTime?)null)
                .ToArray();

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
                if (!await _zones.ContainsPointAsync(slug, origin.Latitude, origin.Longitude, cancellationToken))
                    throw new BadRequestException("El punto de partida tiene que estar dentro de su zona.");

                result[slug] = (origin.Latitude, origin.Longitude);
            }

            return result;
        }

        // One trip per zone and located place, starting at the zone center or at the point the user chose in it.
        // Estimate the time if routing is unavailable.

        private static ScoringRequest BuildRequest(CalculateScoresRequest request)
        {
            var criteria = request.Criteria
                .Where(c => ScoringEngine.IsKnownCriterion(c.Key) && c.Value is "baja" or "media" or "alta")
                .ToDictionary(c => c.Key, c => c.Value!);

            var places = request.Places
                .Select(place => new UserPlace(place.Name.Trim(), place.Latitude, place.Longitude, place.Priority, ParseMinutes(place.MaximumTravelTime)))
                .ToList();

            var (operation, maximumPurchase, housingType) = HousingScoring.Validate(
                request.Operation, request.MaximumPurchasePriceUsd, request.HousingType);

            return new ScoringRequest(
                criteria, places, ParseRent(request.MaximumRent), operation, maximumPurchase, housingType,
                HousingScoring.ValidateSurface(request.AreaM2), HousingScoring.ValidateRooms(request.Rooms));
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
                result.Zone.Slug,
                result.Zone.Name,
                result.Score,
                result.Components.Select(component => new ScoreComponentResponse(component.Criterion, component.Priority, component.Score, component.Details)).ToList(),
                result.Limits.Select(limit => new ScoreLimitResponse(limit.Type, limit.IsSatisfied, limit.Details)).ToList(),
                result.Places.Select(place => new PlaceTravelTimeResponse(
                    place.Name, place.Minutes, place.MaximumMinutes, place.IsEstimated, place.DistanceKm, place.ByBicycle)).ToList(),
                HousingScoring.Rent(result.Zone, request.HousingType, request.AreaM2, request.Rooms),
                HousingScoring.Sale(result.Zone, request.HousingType, request.AreaM2, request.Rooms));
    }
}

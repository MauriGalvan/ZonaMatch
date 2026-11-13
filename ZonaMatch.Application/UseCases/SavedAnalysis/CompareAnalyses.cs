using System.Text.RegularExpressions;
using System.Text.Json;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;
using ScoreLimitResponse = ZonaMatch.Application.DTOs.Scoring.Responses.ScoreLimitResponse;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    /// <summary>
    /// Compares the results saved with each analysis (personal or group), so an analysis and its recalculation show
    /// what changed. Analyses saved before results were kept are recalculated with the criteria saved with them.
    /// </summary>
    public sealed class CompareAnalyses
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private const int MaxAnalyses = 5;
        private const int MaxZones = 10;

        private readonly IAnalysisRepository _analysisRepository;
        private readonly IZoneProfilesRepository _zoneProfilesRepository;
        private readonly IRoutingClient _routingClient;

        public CompareAnalyses(
            IAnalysisRepository analysisRepository,
            IZoneProfilesRepository zoneProfilesRepository,
            IRoutingClient routingClient)
        {
            _analysisRepository = analysisRepository;
            _zoneProfilesRepository = zoneProfilesRepository;
            _routingClient = routingClient;
        }

        public async Task<AnalysisComparisonResponse> ExecuteAsync(
            Guid userId,
            CompareAnalysesRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.AnalysisIds.Count is < 2 or > MaxAnalyses)
                throw new BadRequestException("Elegí entre dos y cinco análisis para comparar.");

            if (request.AnalysisIds.Distinct().Count() != request.AnalysisIds.Count)
                throw new BadRequestException("No se puede comparar el mismo análisis más de una vez.");

            var ownedAnalyses = await _analysisRepository.GetForUserAsync(userId, cancellationToken);
            var byId = ownedAnalyses.ToDictionary(analysis => analysis.Id);
            if (request.AnalysisIds.Any(id => !byId.ContainsKey(id)))
                throw new NotFoundException("El análisis no existe.");

            var selectedAnalyses = request.AnalysisIds.Select(id => byId[id]).ToArray();
            if (selectedAnalyses.Any(analysis => analysis.Context?.Type == ContextType.InvestorAnalysis))
                throw new BadRequestException("Por ahora solo se comparan análisis de hogar.");
            var analysesWithoutZones = selectedAnalyses.Where(analysis => analysis.Zones.Count == 0).ToArray();
            if (analysesWithoutZones.Length > 0)
                throw new BadRequestException("Todos los análisis elegidos deben tener al menos una zona.");

            var analysesWithTooManyZones = selectedAnalyses.Where(analysis => analysis.Zones.Count > MaxZones).ToArray();
            if (analysesWithTooManyZones.Length > 0)
                throw new BadRequestException("Cada análisis puede incluir hasta diez zonas en la comparación.");

            var personalAnalyses = selectedAnalyses
                .Where(analysis => analysis.GroupScoresJson is null
                                   && AnalysisResultCalculator.Deserialize(analysis.ResultJson) is null)
                .ToArray();
            var selectedPersonalZones = personalAnalyses
                .SelectMany(analysis => analysis.Zones)
                .GroupBy(zone => zone.ZoneSlug, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();

            var zonesBySlug = new Dictionary<string, ZoneProfile>(StringComparer.Ordinal);
            ScoringEngine? engine = null;
            if (personalAnalyses.Length > 0)
            {
                var universe = await _zoneProfilesRepository.ListAsync(cancellationToken);
                zonesBySlug = universe.ToDictionary(zone => zone.Slug, StringComparer.Ordinal);
                var unavailableZones = selectedPersonalZones
                    .Where(zone => !zonesBySlug.ContainsKey(zone.ZoneSlug))
                    .Select(zone => zone.Name)
                    .ToArray();
                if (unavailableZones.Length > 0)
                    throw new BadRequestException($"No hay datos para calcular estas zonas: {string.Join(", ", unavailableZones)}.");

                engine = new ScoringEngine(universe);
            }

            using var routeLimit = new SemaphoreSlim(ZoneTripTimes.ConcurrentTrips);

            var calculations = await Task.WhenAll(selectedAnalyses.Select(async analysis =>
            {
                if (analysis.GroupScoresJson is { } groupRankingJson)
                    return FromGroupSnapshot(analysis, groupRankingJson);

                if (AnalysisResultCalculator.Deserialize(analysis.ResultJson) is { } saved)
                    return new ComparedAnalysisResponse(
                        analysis.Id,
                        analysis.Name,
                        saved.TravelMode,
                        saved.Zones.Select(ToComparedZone).ToArray());

                var context = analysis.Context;
                var request = BuildRequest(context);
                var mode = TravelModes.FromMobility(context.Mobility ?? []);
                var analysisZones = analysis.Zones
                    .OrderBy(zone => zone.Order)
                    .Select(zone => zonesBySlug[zone.ZoneSlug])
                    .ToArray();
                var travelTimes = await ZoneTripTimes.MeasureAsync(
                    _routingClient, analysisZones, request.Places, mode, routeLimit, cancellationToken,
                    withBicycle: TravelModes.HasBicycle(context.Mobility));
                var rankedZones = analysisZones
                    .Select(zone => ToDto(engine!.Calculate(zone, request, travelTimes[zone.Slug]), request))
                    .OrderByDescending(zone => zone.Score.HasValue)
                    .ThenByDescending(zone => zone.Score)
                    .ThenBy(zone => zone.Name, StringComparer.CurrentCulture)
                    .ToArray();

                return new ComparedAnalysisResponse(
                    analysis.Id,
                    analysis.Name,
                    TravelModes.ToCode(mode),
                    rankedZones.Select(ToComparedZone).ToArray(),
                    null);
            }));

            if (calculations.All(result => result.Zones.All(zone => zone.Score is null)))
                throw new BadRequestException("No hay datos suficientes para puntuar las zonas de los análisis elegidos.");

            return new AnalysisComparisonResponse(calculations);
        }

        private static ComparedAnalysisResponse FromGroupSnapshot(Analysis analysis, string serializedRanking)
        {
            GroupRankingResponse? snapshot;
            try
            {
                snapshot = GroupRankingSnapshotJson.Deserialize(serializedRanking, JsonOptions);
            }
            catch (JsonException)
            {
                throw new BadRequestException($"El análisis de grupo '{analysis.Name}' no tiene un resultado válido guardado.");
            }

            if (snapshot?.Zones is not { } snapshotZones)
                throw new BadRequestException($"El análisis de grupo '{analysis.Name}' no tiene un resultado válido guardado.");

            var scoresBySlug = snapshotZones
                .GroupBy(zone => zone.Slug, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var savedZones = analysis.Zones.OrderBy(zone => zone.Order).ToArray();
            var missingSlugs = savedZones
                .Where(zone => !scoresBySlug.ContainsKey(zone.ZoneSlug))
                .Select(zone => zone.ZoneSlug)
                .ToArray();
            if (missingSlugs.Length > 0)
                throw new BadRequestException(
                    $"El análisis de grupo '{analysis.Name}' no tiene puntajes guardados para todas sus zonas. Volvé a calcular y guardar ese ranking.");

            var rankedGroupZones = savedZones
                .Select(zone => scoresBySlug[zone.ZoneSlug])
                .OrderByDescending(zone => zone.Score.HasValue)
                .ThenByDescending(zone => zone.Score)
                .ThenBy(zone => zone.Name, StringComparer.CurrentCulture)
                .ToArray();
            var legacyZones = rankedGroupZones
                .Select(ToDto)
                .ToArray();

            return new ComparedAnalysisResponse(
                analysis.Id,
                analysis.Name,
                "grupo",
                legacyZones.Select(ToComparedZone).ToArray(),
                snapshot with { Zones = rankedGroupZones, NearbyZones = [] });
        }

        private static ScoringRequest BuildRequest(Context context)
        {
            var criteria = context.Criteria
                .Where(criterion => ScoringEngine.IsKnownCriterion(criterion.CriterionId)
                                    && criterion.Priority is "baja" or "media" or "alta")
                .ToDictionary(criterion => criterion.CriterionId, criterion => criterion.Priority, StringComparer.Ordinal);

            var places = context.Places
                .OrderBy(point => point.Order)
                .Select(point => new UserPlace(
                    point.Name,
                    point.Latitude,
                    point.Longitude,
                    point.Priority,
                    ParseMinutes(point.MaximumTravelTime)))
                .ToArray();

            return new ScoringRequest(
                criteria, places, ParseRent(context.MaximumRent), context.Operation, context.MaximumPurchasePriceUsd, context.HousingType,
                context.AreaM2, context.Rooms);
        }

        private static int ParseMinutes(string? value)
        {
            var match = Regex.Match(value ?? string.Empty, @"\d+");
            return match.Success && int.TryParse(match.Value, out var minutes) && minutes > 0
                ? minutes
                : CalculateScores.DefaultMaximumTravelTime;
        }

        private static decimal? ParseRent(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return decimal.TryParse(digits, out var amount) && amount > 0 ? amount : null;
        }

        private static ScoredZoneResponse ToDto(ZoneScoreResult result, ScoringRequest request) =>
            new(
                result.Zone.Slug,
                result.Zone.Name,
                result.Score,
                result.Components
                    .Select(component => new ScoreComponentResponse(
                        component.Criterion, component.Priority, component.Score, component.Details))
                    .ToArray(),
                result.Limits
                    .Select(limit => new ScoreLimitResponse(limit.Type, limit.IsSatisfied, limit.Details))
                    .ToArray(),
                result.Places
                    .Select(place => new PlaceTravelTimeResponse(
                        place.Name, place.Minutes, place.MaximumMinutes, place.IsEstimated, place.DistanceKm, place.ByBicycle))
                    .ToArray(),
                HousingScoring.Rent(result.Zone, request.HousingType, request.AreaM2, request.Rooms),
                HousingScoring.Sale(result.Zone, request.HousingType, request.AreaM2, request.Rooms));

        private static ScoredZoneResponse ToDto(RankedZoneResponse result) =>
            new(
                result.Slug,
                result.Name,
                result.Score,
                result.Components?.Select(component => new ScoreComponentResponse(
                    component.Criterion,
                    "ponderado",
                    component.Score,
                    component.Details)).ToArray() ?? [],
                result.Limits?.Select(limit => new ScoreLimitResponse(
                    limit.Type,
                    limit.IsMet,
                    limit.Details)).ToArray() ?? [],
                result.Places?.Select(place => new PlaceTravelTimeResponse(
                    $"{place.ParticipantName}: {place.Name}",
                    place.Minutes,
                    place.MaximumMinutes,
                    place.IsEstimated)).ToArray() ?? [],
                result.Rent is { } rent
                    ? new RentResponse(
                        rent.Average, rent.AvailableProperties, rent.Expenses, rent.MonthlyCost, rent.PricePerM2, rent.SurfaceM2, rent.Estimated, rent.Rooms, rent.Distribution, rent.DistributionShares)
                    : null,
                result.Sale is { } sale
                    ? new SaleResponse(
                        sale.AverageUsd, sale.AvailableProperties, sale.Expenses, sale.PricePerM2Usd, sale.SurfaceM2, sale.EstimatedUsd, sale.Rooms, sale.Distribution, sale.DistributionShares)
                    : null);

        private static ComparedZoneResponse ToComparedZone(ScoredZoneResponse zone) =>
            new(
                zone.Slug,
                zone.Name,
                zone.Score,
                zone.Components
                    .Select(component => new ComparedScoreComponentResponse(
                        component.Criterion, component.Priority, component.Score, component.Details))
                    .ToArray(),
                zone.Limits
                    .Select(limit => new ComparedScoreLimitResponse(limit.Type, limit.IsMet, limit.Details))
                    .ToArray(),
                zone.Places
                    .Select(place => new ComparedPlaceTravelTimeResponse(
                        place.Name, place.Minutes, place.MaximumMinutes, place.IsEstimated, place.DistanceKm, place.ByBicycle))
                    .ToArray(),
                zone.Rent is { } rent
                    ? new ComparedRentResponse(
                        rent.Average, rent.AvailableProperties, rent.Expenses, rent.MonthlyCost,
                        rent.PricePerM2, rent.SurfaceM2, rent.Estimated, rent.Rooms, rent.Distribution, rent.DistributionShares)
                    : null,
                zone.Sale is { } sale
                    ? new ComparedSaleResponse(
                        sale.AverageUsd, sale.AvailableProperties, sale.Expenses, sale.PricePerM2Usd,
                        sale.SurfaceM2, sale.EstimatedUsd, sale.Rooms, sale.Distribution, sale.DistributionShares)
                    : null);
    }
}

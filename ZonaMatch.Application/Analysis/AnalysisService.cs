using ZonaMatch.Application.Common;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.PointsOfInterest;
using ZonaMatch.Application.Territory;
using ZonaMatch.Domain.Geo;
using ZonaMatch.Domain.Scoring;
using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Analysis
{
    // ranking personalizado (PBI 34a, 35a, 36a, 28a, 33a, 45a) y combinado en grupo (PBI 58a, 59a)
    public class AnalysisService
    {
        public const int MaxZones = 20;
        public const int MaxParticipants = 5;
        public const int MaxPointsPerParticipant = 10;
        public const int MaxSuggestionCandidates = 10;
        public const int MaxTripsPerWeek = 14;
        public const int MaxImportance = 4;

        private readonly IZoneRepository _zones;
        private readonly IPoiRepository _pois;
        private readonly IndicatorService _indicators;
        private readonly IRoutingService _routing;

        public AnalysisService(IZoneRepository zones, IPoiRepository pois, IndicatorService indicators, IRoutingService routing)
        {
            _zones = zones;
            _pois = pois;
            _indicators = indicators;
            _routing = routing;
        }

        public IReadOnlyList<CriterionInfoDto> GetCriteria()
        {
            return CriterionCatalog.All.Select(criterion => new CriterionInfoDto(criterion.Code, criterion.Name)).ToList();
        }

        public async Task<RankingResultDto> RankAsync(RankingRequest request, CancellationToken cancellationToken)
        {
            Validate(request);

            var slugs = request.ZoneSlugs.Distinct(StringComparer.Ordinal).ToList();
            var selected = await _zones.GetBySlugsAsync(slugs, cancellationToken);
            var missing = slugs.FirstOrDefault(slug => selected.All(zone => zone.Slug != slug));

            if (missing is not null)
            {
                throw new NotFoundException("la zona", missing);
            }

            var candidates = request.IncludeSuggestion
                ? await GetSuggestionCandidatesAsync(selected, cancellationToken)
                : [];
            var evaluated = selected.Concat(candidates).ToList();

            var criteria = request.Participants
                .SelectMany(participant => participant.Criteria)
                .Select(criterion => CriterionCatalog.Get(criterion.Code))
                .DistinctBy(criterion => criterion.Code)
                .ToList();

            var indicatorCodes = criteria
                .Where(criterion => criterion.Kind == CriterionSourceKind.Indicator)
                .Select(criterion => criterion.Reference)
                .ToHashSet(StringComparer.Ordinal);

            if (request.MaxRent is not null)
            {
                indicatorCodes.Add(IndicatorCodes.AverageRent);
            }

            var indicators = await _indicators.ResolveAsync(evaluated, indicatorCodes, cancellationToken);
            var layerCounts = await CountLayersAsync(evaluated, request.RadiusMeters, criteria, cancellationToken);

            var participants = new List<ParticipantEvaluation>();

            foreach (var participant in request.Participants)
            {
                participants.Add(await EvaluateParticipantAsync(participant, evaluated, layerCounts, indicators, cancellationToken));
            }

            var context = new ResultContext(request, criteria, participants, indicators);
            var selectedSlugs = selected.Select(zone => zone.Slug).ToHashSet(StringComparer.Ordinal);

            var ranked = selected
                .Select(zone => (Zone: zone, Score: CombinedScore(zone, participants)))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Zone.Name, StringComparer.Ordinal)
                .Select((item, index) => BuildZoneResult(index + 1, item.Zone, item.Score, context))
                .ToList();

            var worstSelected = ranked.Min(zone => zone.Score);

            // PBI 45a: zona aledaña no elegida que puntúa mejor que alguna de las elegidas
            var suggestion = candidates
                .Where(zone => !selectedSlugs.Contains(zone.Slug))
                .Select(zone => (Zone: zone, Score: CombinedScore(zone, participants)))
                .Where(item => item.Score > worstSelected)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Zone.Name, StringComparer.Ordinal)
                .Select(item => BuildZoneResult(0, item.Zone, item.Score, context))
                .FirstOrDefault();

            return new RankingResultDto(request.RadiusMeters, ranked, suggestion);
        }

        public static string StatusName(RestrictionStatus status) => status switch
        {
            RestrictionStatus.Complies => "cumple",
            RestrictionStatus.NearLimit => "cerca_del_limite",
            RestrictionStatus.Exceeds => "excede",
            _ => "sin_dato",
        };

        private static void Validate(RankingRequest request)
        {
            if (request.ZoneSlugs.Count is 0 or > MaxZones)
            {
                throw new ArgumentException($"Hay que elegir entre 1 y {MaxZones} zonas.", nameof(request));
            }

            if (request.Participants.Count is 0 or > MaxParticipants)
            {
                throw new ArgumentException($"Se admiten entre 1 y {MaxParticipants} participantes.", nameof(request));
            }

            PoiQueryService.ValidateRadius(request.RadiusMeters);

            if (request.MaxRent is <= 0)
            {
                throw new ArgumentException("El alquiler máximo debe ser positivo.", nameof(request));
            }

            foreach (var participant in request.Participants)
            {
                ValidateParticipant(participant);
            }
        }

        private static void ValidateParticipant(ParticipantRequest participant)
        {
            if (participant.Weight <= 0)
            {
                throw new ArgumentException($"El peso de {participant.Name} debe ser positivo.", nameof(participant));
            }

            if (participant.Criteria.Count == 0)
            {
                throw new ArgumentException($"{participant.Name} tiene que elegir al menos un criterio.", nameof(participant));
            }

            if (participant.Points.Count > MaxPointsPerParticipant)
            {
                throw new ArgumentException($"Se admiten hasta {MaxPointsPerParticipant} puntos por perfil.", nameof(participant));
            }

            foreach (var criterion in participant.Criteria)
            {
                CriterionCatalog.Get(criterion.Code);
            }

            foreach (var point in participant.Points)
            {
                if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
                {
                    throw new ArgumentException($"Coordenadas inválidas en '{point.Name}'.", nameof(participant));
                }

                if (point.TripsPerWeek is < 0 or > MaxTripsPerWeek)
                {
                    throw new ArgumentException($"La frecuencia de '{point.Name}' debe estar entre 0 y {MaxTripsPerWeek}.", nameof(participant));
                }

                if (point.Importance is < 1 or > MaxImportance)
                {
                    throw new ArgumentException($"La importancia de '{point.Name}' debe estar entre 1 y {MaxImportance}.", nameof(participant));
                }

                if (point.MaxMinutes is <= 0)
                {
                    throw new ArgumentException($"El tiempo máximo de '{point.Name}' debe ser positivo.", nameof(participant));
                }
            }
        }

        private async Task<IReadOnlyList<ZoneData>> GetSuggestionCandidatesAsync(
            IReadOnlyList<ZoneData> selected,
            CancellationToken cancellationToken)
        {
            var selectedSlugs = selected.Select(zone => zone.Slug).ToHashSet(StringComparer.Ordinal);
            var neighborSlugs = new List<string>();

            foreach (var zone in selected)
            {
                foreach (var neighbor in await _zones.GetNeighborsAsync(zone.Id, cancellationToken))
                {
                    if (!selectedSlugs.Contains(neighbor.Slug) && !neighborSlugs.Contains(neighbor.Slug))
                    {
                        neighborSlugs.Add(neighbor.Slug);
                    }
                }
            }

            return neighborSlugs.Count == 0
                ? []
                : await _zones.GetBySlugsAsync(neighborSlugs.Take(MaxSuggestionCandidates).ToList(), cancellationToken);
        }

        private async Task<IReadOnlyDictionary<int, Dictionary<string, int>>> CountLayersAsync(
            IReadOnlyList<ZoneData> zones,
            int radiusMeters,
            IReadOnlyList<CriterionDefinition> criteria,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, Dictionary<string, int>>();
            var needsCounts = criteria.Any(criterion => criterion.Kind != CriterionSourceKind.Indicator);

            foreach (var zone in zones)
            {
                var counts = needsCounts
                    ? await _pois.CountWithinAsync(zone.Center, radiusMeters, cancellationToken)
                    : [];

                result[zone.Id] = counts
                    .GroupBy(count => count.RootCategoryCode, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Sum(count => count.Count), StringComparer.Ordinal);
            }

            return result;
        }

        private async Task<ParticipantEvaluation> EvaluateParticipantAsync(
            ParticipantRequest participant,
            IReadOnlyList<ZoneData> zones,
            IReadOnlyDictionary<int, Dictionary<string, int>> layerCounts,
            IReadOnlyDictionary<int, IReadOnlyDictionary<string, IndicatorValueDto>> indicators,
            CancellationToken cancellationToken)
        {
            var hasPoints = participant.Points.Count > 0;

            var weighted = participant.Criteria
                .DistinctBy(criterion => criterion.Code)
                .Select(criterion =>
                {
                    var definition = CriterionCatalog.Get(criterion.Code);
                    // con puntos importantes, movilidad pasa a medir minutos: menos es mejor
                    var direction = definition.Kind == CriterionSourceKind.Mobility && hasPoints
                        ? CriterionDirection.LowerIsBetter
                        : definition.Direction;
                    return (Definition: definition, Criterion: new WeightedCriterion(definition.Code, criterion.Priority, direction));
                })
                .ToList();

            var values = new List<ZoneCriterionValues>();
            var weeklyMinutes = new Dictionary<int, double>();
            var restrictions = new Dictionary<int, List<RestrictionResultDto>>();

            foreach (var zone in zones)
            {
                var trips = new List<(ImportantPointRequest Point, double Minutes)>();

                foreach (var point in participant.Points)
                {
                    var destination = GeometryNormalizer.Factory.CreatePoint(
                        new NetTopologySuite.Geometries.Coordinate(point.Longitude, point.Latitude));
                    var estimate = await _routing.EstimateAsync(zone.Center, destination, point.Mode, cancellationToken);
                    trips.Add((point, estimate.Minutes));
                }

                var zoneValues = new Dictionary<string, double?>(StringComparer.Ordinal);

                foreach (var (definition, _) in weighted)
                {
                    zoneValues[definition.Code] = definition.Kind switch
                    {
                        CriterionSourceKind.PoiLayer => layerCounts[zone.Id].GetValueOrDefault(definition.Reference),
                        CriterionSourceKind.Indicator => indicators[zone.Id].TryGetValue(definition.Reference, out var indicator)
                            ? (double)indicator.Value
                            : null,
                        _ => hasPoints
                            ? WeightedMinutes(trips)
                            : layerCounts[zone.Id].GetValueOrDefault(CriterionCatalog.TransportLayer),
                    };
                }

                values.Add(new ZoneCriterionValues(zone.Id, zoneValues));
                weeklyMinutes[zone.Id] = TravelTimeEstimator.WeeklyMinutes(trips.Select(trip => (trip.Minutes, trip.Point.TripsPerWeek)));
                restrictions[zone.Id] = trips
                    .Where(trip => trip.Point.MaxMinutes is not null)
                    .Select(trip => new RestrictionResultDto(
                        "max_travel_time",
                        $"Tiempo a {trip.Point.Name}",
                        participant.Name,
                        StatusName(RestrictionEvaluator.Evaluate(trip.Minutes, trip.Point.MaxMinutes!.Value)),
                        trip.Minutes,
                        trip.Point.MaxMinutes.Value))
                    .ToList();
            }

            var scores = ZoneScoreCalculator
                .Calculate(values, weighted.Select(item => item.Criterion).ToList())
                .ToDictionary(score => score.ZoneId);

            return new ParticipantEvaluation(participant, hasPoints, scores, weeklyMinutes, restrictions);
        }

        // un punto diario y muy importante pesa más que uno semanal (PBI 30a)
        public static double WeightedMinutes(IReadOnlyList<(ImportantPointRequest Point, double Minutes)> trips)
        {
            var weights = trips.Select(trip => (double)trip.Point.Importance * Math.Max(trip.Point.TripsPerWeek, 1)).ToList();
            return Math.Round(trips.Select((trip, index) => trip.Minutes * weights[index]).Sum() / weights.Sum(), 1);
        }

        private static double CombinedScore(ZoneData zone, IReadOnlyList<ParticipantEvaluation> participants)
        {
            return ProfileCombiner.Combine(participants
                .Select(participant => (participant.Request.Weight, participant.Scores[zone.Id].Score))
                .ToList());
        }

        private static RankedZoneDto BuildZoneResult(int position, ZoneData zone, double score, ResultContext context)
        {
            var participants = context.Participants;
            var totalWeight = participants.Sum(participant => participant.Request.Weight);
            var coverage = Math.Round(
                participants.Sum(participant => participant.Request.Weight * participant.Scores[zone.Id].Coverage) / totalWeight,
                2);

            var breakdown = context.Criteria
                .Select(criterion => new CriterionResultDto(
                    criterion.Code,
                    criterion.Name,
                    CombinedCriterionScore(zone.Id, criterion.Code, participants),
                    DataNote(criterion, zone, context)))
                .ToList();

            var restrictions = participants.SelectMany(participant => participant.Restrictions[zone.Id]).ToList();

            if (context.Request.MaxRent is not null)
            {
                var rent = context.Indicators[zone.Id].TryGetValue(IndicatorCodes.AverageRent, out var value)
                    ? (double?)value.Value
                    : null;
                restrictions.Insert(0, new RestrictionResultDto(
                    "max_rent",
                    "Alquiler",
                    null,
                    StatusName(RestrictionEvaluator.Evaluate(rent, context.Request.MaxRent.Value)),
                    rent,
                    context.Request.MaxRent.Value));
            }

            var participantResults = participants
                .Select(participant => new ParticipantResultDto(
                    participant.Request.Name,
                    participant.Scores[zone.Id].Score,
                    participant.WeeklyMinutes[zone.Id]))
                .ToList();

            return new RankedZoneDto(
                position,
                zone.Slug,
                zone.Name,
                zone.ParentName,
                score,
                coverage,
                breakdown,
                participantResults,
                restrictions);
        }

        private static double? CombinedCriterionScore(int zoneId, string code, IReadOnlyList<ParticipantEvaluation> participants)
        {
            var scored = participants
                .Select(participant => (
                    participant.Request.Weight,
                    Score: participant.Scores[zoneId].Breakdown.FirstOrDefault(item => item.Code == code)?.Score))
                .Where(item => item.Score is not null)
                .ToList();

            return scored.Count == 0
                ? null
                : Math.Round(scored.Sum(item => item.Weight * item.Score!.Value) / scored.Sum(item => item.Weight), 1);
        }

        private static string DataNote(CriterionDefinition criterion, ZoneData zone, ResultContext context)
        {
            switch (criterion.Kind)
            {
                case CriterionSourceKind.Indicator:
                    if (!context.Indicators[zone.Id].TryGetValue(criterion.Reference, out var indicator))
                    {
                        return "sin dato";
                    }

                    var origin = indicator.Inherited ? $"dato de {indicator.Level}" : "directo";
                    return $"{origin} · {indicator.Source} ({indicator.ReferenceDate.Year})";
                case CriterionSourceKind.PoiLayer:
                    return $"lugares en {context.Request.RadiusMeters} m";
                default:
                    return context.Participants.Any(participant => participant.HasPoints)
                        ? "tiempo estimado a tus puntos"
                        : $"transporte en {context.Request.RadiusMeters} m";
            }
        }

        private sealed record ParticipantEvaluation(
            ParticipantRequest Request,
            bool HasPoints,
            IReadOnlyDictionary<int, ZoneScore> Scores,
            IReadOnlyDictionary<int, double> WeeklyMinutes,
            IReadOnlyDictionary<int, List<RestrictionResultDto>> Restrictions);

        private sealed record ResultContext(
            RankingRequest Request,
            IReadOnlyList<CriterionDefinition> Criteria,
            IReadOnlyList<ParticipantEvaluation> Participants,
            IReadOnlyDictionary<int, IReadOnlyDictionary<string, IndicatorValueDto>> Indicators);
    }
}

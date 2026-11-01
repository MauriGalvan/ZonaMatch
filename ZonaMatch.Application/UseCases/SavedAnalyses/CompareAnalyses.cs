using System.Text.RegularExpressions;
using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
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
        private const int ParallelTrips = 6;

        private readonly IAnalisisRepository _analysisRepository;
        private readonly IPerfilesZonaRepository _zoneProfilesRepository;
        private readonly IRuteoClient _routingClient;

        public CompareAnalyses(
            IAnalisisRepository analysisRepository,
            IPerfilesZonaRepository zoneProfilesRepository,
            IRuteoClient routingClient)
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

            var ownedAnalyses = await _analysisRepository.ListarDeUsuarioAsync(userId, cancellationToken);
            var byId = ownedAnalyses.ToDictionary(analysis => analysis.Id);
            if (request.AnalysisIds.Any(id => !byId.ContainsKey(id)))
                throw new NotFoundException("El análisis no existe.");

            var selectedAnalyses = request.AnalysisIds.Select(id => byId[id]).ToArray();
            var analysesWithoutZones = selectedAnalyses.Where(analysis => analysis.Zonas.Count == 0).ToArray();
            if (analysesWithoutZones.Length > 0)
                throw new BadRequestException("Todos los análisis elegidos deben tener al menos una zona.");

            var analysesWithTooManyZones = selectedAnalyses.Where(analysis => analysis.Zonas.Count > MaxZones).ToArray();
            if (analysesWithTooManyZones.Length > 0)
                throw new BadRequestException("Cada análisis puede incluir hasta diez zonas en la comparación.");

            var personalAnalyses = selectedAnalyses
                .Where(analysis => analysis.PuntajesEnGrupoJson is null
                                   && AnalysisResultCalculator.Deserialize(analysis.ResultadoJson) is null)
                .ToArray();
            var selectedPersonalZones = personalAnalyses
                .SelectMany(analysis => analysis.Zonas)
                .GroupBy(zone => zone.ZonaSlug, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();

            var zonesBySlug = new Dictionary<string, ZoneProfile>(StringComparer.Ordinal);
            ScoringEngine? motor = null;
            if (personalAnalyses.Length > 0)
            {
                var universe = await _zoneProfilesRepository.ListarAsync(cancellationToken);
                zonesBySlug = universe.ToDictionary(zone => zone.Slug, StringComparer.Ordinal);
                var unavailableZones = selectedPersonalZones
                    .Where(zone => !zonesBySlug.ContainsKey(zone.ZonaSlug))
                    .Select(zone => zone.Nombre)
                    .ToArray();
                if (unavailableZones.Length > 0)
                    throw new BadRequestException($"No hay datos para calcular estas zonas: {string.Join(", ", unavailableZones)}.");

                motor = new ScoringEngine(universe);
            }

            using var routeLimit = new SemaphoreSlim(ParallelTrips);

            var calculations = await Task.WhenAll(selectedAnalyses.Select(async analysis =>
            {
                if (analysis.PuntajesEnGrupoJson is { } groupRankingJson)
                    return FromGroupSnapshot(analysis, groupRankingJson);

                if (AnalysisResultCalculator.Deserialize(analysis.ResultadoJson) is { } saved)
                    return new ComparedAnalysisResponse(
                        analysis.Id,
                        analysis.Nombre,
                        saved.TravelMode,
                        saved.Zonas.Select(ToComparedZone).ToArray());

                var context = analysis.Contexto;
                var request = BuildRequest(context);
                var mode = TravelModes.FromMobility(context.Movilidad ?? []);
                var analysisZones = analysis.Zonas
                    .OrderBy(zone => zone.Orden)
                    .Select(zone => zonesBySlug[zone.ZonaSlug])
                    .ToArray();
                var travelTimes = await MeasureTripsAsync(analysisZones, request.Lugares, mode, routeLimit, cancellationToken);
                var rankedZones = analysisZones
                    .Select(zone => ToDto(motor!.Calcular(zone, request, travelTimes[zone.Slug]), request))
                    .OrderByDescending(zone => zone.Puntaje.HasValue)
                    .ThenByDescending(zone => zone.Puntaje)
                    .ThenBy(zone => zone.Nombre, StringComparer.CurrentCulture)
                    .ToArray();

                return new ComparedAnalysisResponse(
                    analysis.Id,
                    analysis.Nombre,
                    TravelModes.ToCode(mode),
                    rankedZones.Select(ToComparedZone).ToArray(),
                    null);
            }));

            if (calculations.All(result => result.Zones.All(zone => zone.Score is null)))
                throw new BadRequestException("No hay datos suficientes para puntuar las zonas de los análisis elegidos.");

            return new AnalysisComparisonResponse(calculations);
        }

        private static ComparedAnalysisResponse FromGroupSnapshot(Analisis analysis, string serializedRanking)
        {
            GroupRankingResponse? snapshot;
            try
            {
                snapshot = GroupRankingSnapshotJson.Deserialize(serializedRanking, JsonOptions);
            }
            catch (JsonException)
            {
                throw new BadRequestException($"El análisis de grupo '{analysis.Nombre}' no tiene un resultado válido guardado.");
            }

            if (snapshot?.Zones is not { } snapshotZones)
                throw new BadRequestException($"El análisis de grupo '{analysis.Nombre}' no tiene un resultado válido guardado.");

            var scoresBySlug = snapshotZones
                .GroupBy(zone => zone.Slug, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var savedZones = analysis.Zonas.OrderBy(zone => zone.Orden).ToArray();
            var missingSlugs = savedZones
                .Where(zone => !scoresBySlug.ContainsKey(zone.ZonaSlug))
                .Select(zone => zone.ZonaSlug)
                .ToArray();
            if (missingSlugs.Length > 0)
                throw new BadRequestException(
                    $"El análisis de grupo '{analysis.Nombre}' no tiene puntajes guardados para todas sus zonas. Volvé a calcular y guardar ese ranking.");

            var rankedGroupZones = savedZones
                .Select(zone => scoresBySlug[zone.ZonaSlug])
                .OrderByDescending(zone => zone.Score.HasValue)
                .ThenByDescending(zone => zone.Score)
                .ThenBy(zone => zone.Name, StringComparer.CurrentCulture)
                .ToArray();
            var legacyZones = rankedGroupZones
                .Select(ToDto)
                .ToArray();

            return new ComparedAnalysisResponse(
                analysis.Id,
                analysis.Nombre,
                "grupo",
                legacyZones.Select(ToComparedZone).ToArray(),
                snapshot with { Zones = rankedGroupZones, NearbyZones = [] });
        }

        private async Task<Dictionary<string, IReadOnlyList<TravelTime?>>> MeasureTripsAsync(
            IReadOnlyList<ZoneProfile> zones,
            IReadOnlyList<UserPlace> places,
            TravelMode mode,
            SemaphoreSlim limiter,
            CancellationToken cancellationToken)
        {
            async Task<TravelTime?> MeasureAsync(ZoneProfile zone, UserPlace place)
            {
                if (!place.TieneUbicacion)
                    return null;

                await limiter.WaitAsync(cancellationToken);
                try
                {
                    return await _routingClient.CalcularAsync(
                               zone.Latitud, zone.Longitud,
                               place.Latitud!.Value, place.Longitud!.Value,
                               mode, cancellationToken)
                           ?? TravelModes.Estimate(
                               mode, zone.Latitud, zone.Longitud,
                               place.Latitud!.Value, place.Longitud!.Value);
                }
                finally
                {
                    limiter.Release();
                }
            }

            var zoneTasks = zones.Select(async zone =>
                (zone.Slug, Times: await Task.WhenAll(places.Select(place => MeasureAsync(zone, place)))));
            var results = await Task.WhenAll(zoneTasks);
            return results.ToDictionary(
                result => result.Slug,
                result => (IReadOnlyList<TravelTime?>)result.Times,
                StringComparer.Ordinal);
        }

        private static ScoringRequest BuildRequest(Contexto context)
        {
            var criteria = context.Criterios
                .Where(criterion => ScoringEngine.EsCriterioConocido(criterion.CriterioId)
                                    && criterion.Prioridad is "baja" or "media" or "alta")
                .ToDictionary(criterion => criterion.CriterioId, criterion => criterion.Prioridad, StringComparer.Ordinal);

            var places = context.Puntos
                .OrderBy(point => point.Orden)
                .Select(point => new UserPlace(
                    point.Nombre,
                    point.Latitude,
                    point.Longitude,
                    point.Prioridad,
                    ParseMinutes(point.TiempoMax)))
                .ToArray();

            return new ScoringRequest(
                criteria, places, ParseRent(context.AlquilerMaximo), context.Operacion, context.CompraMaximaUsd, context.TipoVivienda,
                context.SuperficieM2);
        }

        private static int ParseMinutes(string? value)
        {
            var match = Regex.Match(value ?? string.Empty, @"\d+");
            return match.Success && int.TryParse(match.Value, out var minutes) && minutes > 0
                ? minutes
                : CalcularPuntajes.TiempoMaximoPorDefecto;
        }

        private static decimal? ParseRent(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            return decimal.TryParse(digits, out var amount) && amount > 0 ? amount : null;
        }

        private static PuntajeZonaDto ToDto(ZoneScoreResult result, ScoringRequest request) =>
            new(
                result.Zona.Slug,
                result.Zona.Nombre,
                result.Puntaje,
                result.Componentes
                    .Select(component => new ComponentePuntajeDto(
                        component.Criterio, component.Prioridad, component.Puntaje, component.Detalle))
                    .ToArray(),
                result.Limites
                    .Select(limit => new LimitePuntajeDto(limit.Tipo, limit.Cumple, limit.Detalle))
                    .ToArray(),
                result.Lugares
                    .Select(place => new TiempoLugarDto(
                        place.Nombre, place.Minutos, place.MaximoMinutos, place.Estimado, place.DistanciaKm))
                    .ToArray(),
                Vivienda.Alquiler(result.Zona, request.TipoVivienda, request.SuperficieM2),
                Vivienda.Compra(result.Zona, request.TipoVivienda, request.SuperficieM2));

        private static PuntajeZonaDto ToDto(RankedZoneResponse result) =>
            new(
                result.Slug,
                result.Name,
                result.Score,
                result.Components?.Select(component => new ComponentePuntajeDto(
                    component.Criterion,
                    "ponderado",
                    component.Score,
                    component.Details)).ToArray() ?? [],
                result.Limits?.Select(limit => new LimitePuntajeDto(
                    limit.Type,
                    limit.IsMet,
                    limit.Details)).ToArray() ?? [],
                result.Places?.Select(place => new TiempoLugarDto(
                    $"{place.ParticipantName}: {place.Name}",
                    place.Minutes,
                    place.MaximumMinutes,
                    place.IsEstimated)).ToArray() ?? [],
                result.Rent is { } rent
                    ? new AlquilerZonaDto(
                        rent.Average, rent.AvailableProperties, rent.Expenses, rent.MonthlyCost, rent.PricePerM2, rent.SurfaceM2, rent.Estimated)
                    : null,
                result.Sale is { } sale
                    ? new CompraZonaDto(
                        sale.AverageUsd, sale.AvailableProperties, sale.Expenses, sale.PricePerM2Usd, sale.SurfaceM2, sale.EstimatedUsd)
                    : null);

        private static ComparedZoneResponse ToComparedZone(PuntajeZonaDto zone) =>
            new(
                zone.Slug,
                zone.Nombre,
                zone.Puntaje,
                zone.Componentes
                    .Select(component => new ComparedScoreComponentResponse(
                        component.Criterio, component.Prioridad, component.Puntaje, component.Detalle))
                    .ToArray(),
                zone.Limites
                    .Select(limit => new ComparedScoreLimitResponse(limit.Tipo, limit.Cumple, limit.Detalle))
                    .ToArray(),
                zone.Lugares
                    .Select(place => new ComparedPlaceTravelTimeResponse(
                        place.Nombre, place.Minutos, place.MaximoMinutos, place.Estimado, place.DistanciaKm))
                    .ToArray(),
                zone.Alquiler is { } rent
                    ? new ComparedRentResponse(
                        rent.Promedio, rent.PropiedadesDisponibles, rent.Expensas, rent.CostoMensual,
                        rent.PrecioM2, rent.SuperficieM2, rent.Estimado)
                    : null,
                zone.Compra is { } sale
                    ? new ComparedSaleResponse(
                        sale.PromedioUsd, sale.PropiedadesDisponibles, sale.Expensas, sale.PrecioM2Usd,
                        sale.SuperficieM2, sale.EstimadoUsd)
                    : null);
    }
}

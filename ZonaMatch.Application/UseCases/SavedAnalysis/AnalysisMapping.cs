using System.Text.Json;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Application.UseCases.Investor;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    public static class AnalysisMapping
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static AnalysisResponse ToResponse(Analysis analysis, IEnumerable<Analysis>? versions = null) =>
            new(
                analysis.Id,
                analysis.Name,
                analysis.OriginalAnalysisId,
                analysis.Zones
                    .OrderBy(z => z.Order)
                    .Select(z => new AnalysisZoneResponse(
                        z.ZoneSlug, z.Name, z.IsInitial, z.IsAdded,
                        z.HasOrigin ? new AnalysisZoneOriginResponse(z.OriginLatitude!.Value, z.OriginLongitude!.Value) : null))
                    .ToList(),
                AnalysisCriteriaBuilder.ToContextResponse(analysis.Context),
                AnalysisCriteriaBuilder.ToCriteriaDictionary(analysis.Context),
                AnalysisCriteriaBuilder.ToPointsResponse(analysis.Context),
                analysis.CreatedAt,
                analysis.UpdatedAt,
                analysis.GroupScoresJson is null
                    ? null
                    : GroupRankingSnapshotJson.Deserialize(analysis.GroupScoresJson, JsonOptions),
                (versions ?? [])
                    .OrderBy(v => v.CreatedAt)
                    .Select(v => ToResponse(v))
                    .ToList(),
                IsInvestment(analysis) ? null : AnalysisResultCalculator.Deserialize(analysis.ResultJson),
                analysis.ResultGeneratedAt,
                analysis.IsFavorite,
                IsInvestment(analysis) ? AnalysisResponse.InvestorProfile : AnalysisResponse.HouseholdProfile,
                IsInvestment(analysis) ? InvestmentAnalyses.DeserializeResult(analysis.ResultJson) : null);

        private static bool IsInvestment(Analysis analysis) => analysis.Context?.Type == ContextType.InvestorAnalysis;

        // Request zones must exist, be unique, and include at most one initial zone.
        public static async Task<List<AnalysisZone>> BuildZonesAsync(
            IReadOnlyList<AnalysisZoneRequest> zones, IZoneRepository repository, CancellationToken cancellationToken)
        {
            if (zones.Count == 0)
                throw new BadRequestException("Elegí al menos una zona.");

            if (zones.Select(zone => zone.Slug).Distinct(StringComparer.Ordinal).Count() != zones.Count)
                throw new BadRequestException("Hay zonas repetidas en el análisis.");

            if (zones.Count(zone => zone.IsInitial) > 1)
                throw new BadRequestException("Solo puede haber una zona inicial.");

            // A starting point replaces the center of the zone, so it has to be inside it
            foreach (var zone in zones)
            {
                if (zone.Origin is { } origin)
                    await repository.EnsurePointBelongsToZoneAsync(zone.Slug, origin.Latitude, origin.Longitude, cancellationToken);
                else
                    await repository.EnsureZoneExistsAsync(zone.Slug, cancellationToken);
            }

            return zones
                .Select((zone, index) => new AnalysisZone
                {
                    Order = index + 1,
                    ZoneSlug = zone.Slug,
                    Name = zone.Name.Trim(),
                    IsInitial = zone.IsInitial,
                    IsAdded = zone.IsAdded,
                    OriginLatitude = zone.Origin?.Latitude,
                    OriginLongitude = zone.Origin?.Longitude,
                })
                .ToList();
        }
    }
}

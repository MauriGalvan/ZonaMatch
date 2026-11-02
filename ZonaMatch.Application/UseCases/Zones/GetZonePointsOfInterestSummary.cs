using ZonaMatch.Application.DTOs.PointsOfInterest.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Zones;

public sealed class GetZonePointsOfInterestSummary(
    IPointOfInterestRepository pointOfInterestRepository,
    PointOfInterestResponseBuilder responseBuilder)
{
    public async Task<PointsOfInterestSummaryResponse> ExecuteAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);
        var counts = await pointOfInterestRepository.CountByTypeInZoneAsync(slug, cancellationToken);
        return responseBuilder.BuildSummary(radiusMeters: null, counts: counts);
    }
}

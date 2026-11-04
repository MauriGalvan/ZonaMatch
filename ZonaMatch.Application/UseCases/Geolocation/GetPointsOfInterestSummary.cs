using ZonaMatch.Application.DTOs.PointOfInterest.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetPointsOfInterestSummary(
    IPointOfInterestRepository pointOfInterestRepository,
    PointOfInterestResponseBuilder responseBuilder)
{
    public async Task<PointsOfInterestSummaryResponse> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude, radiusMeters);
        GeolocationRequestValidator.ValidatePointOfInterestRadius(radiusMeters);

        var counts = await pointOfInterestRepository.CountByTypeAsync(
            latitude, longitude, radiusMeters, cancellationToken);
        return responseBuilder.BuildSummary(radiusMeters, counts);
    }
}

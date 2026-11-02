using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetNearbyPointsOfInterest(
    IPointOfInterestRepository pointOfInterestRepository,
    PointOfInterestResponseBuilder responseBuilder)
{
    public async Task<PuntosInteresCercanosDto> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? categories,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude, radiusMeters);
        GeolocationRequestValidator.ValidatePointOfInterestRadius(radiusMeters);

        if (!PointOfInterestCategory.TryParseList(categories, out var categoryCodes, out var error))
            throw new BadRequestException(error);

        var points = await pointOfInterestRepository.GetCercanosAsync(
            latitude,
            longitude,
            radiusMeters,
            categoryCodes,
            PointOfInterestResponseBuilder.MaximumPoints + 1,
            cancellationToken);

        return responseBuilder.BuildPoints(points);
    }
}

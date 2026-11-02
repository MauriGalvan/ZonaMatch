using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetPointsOfInterestSummary(
    IPuntoInteresRepository pointOfInterestRepository,
    PointOfInterestResponseBuilder responseBuilder)
{
    public async Task<ResumenPuntosInteresDto> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude, radiusMeters);
        GeolocationRequestValidator.ValidatePointOfInterestRadius(radiusMeters);

        var counts = await pointOfInterestRepository.ContarPorTipoAsync(
            latitude, longitude, radiusMeters, cancellationToken);
        return responseBuilder.BuildSummary(radiusMeters, counts);
    }
}

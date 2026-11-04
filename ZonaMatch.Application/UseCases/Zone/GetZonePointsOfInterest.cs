using ZonaMatch.Application.DTOs.PointOfInterest.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Common;

namespace ZonaMatch.Application.UseCases.Zone;

public sealed class GetZonePointsOfInterest(
    IPointOfInterestRepository pointOfInterestRepository,
    PointOfInterestResponseBuilder responseBuilder)
{
    public async Task<NearbyPointsOfInterestResponse> ExecuteAsync(
        string slug,
        string? categories,
        CancellationToken cancellationToken = default)
    {
        ZoneQueryPolicy.ValidateSlug(slug);

        if (!PointOfInterestCategory.TryParseList(categories, out var categoryCodes, out var error))
            throw new BadRequestException(error);

        // One extra row indicates whether the result was truncated.
        var points = await pointOfInterestRepository.GetInZoneAsync(
            slug,
            categoryCodes,
            PointOfInterestResponseBuilder.MaximumPoints + 1,
            cancellationToken);

        return responseBuilder.BuildPoints(points);
    }
}

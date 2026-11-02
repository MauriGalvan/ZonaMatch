using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetDistrictByLocation(LocationBoundaryLookup boundaryLookup)
{
    public async Task<DistrictResponse> ExecuteAsync(double latitude, double longitude)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);
        return await boundaryLookup.GetDistrictAsync(latitude, longitude)
            ?? throw new NotFoundException("No se encontro un partido para esa ubicacion.");
    }
}

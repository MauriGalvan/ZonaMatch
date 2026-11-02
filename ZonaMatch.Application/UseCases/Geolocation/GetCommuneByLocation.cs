using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetCommuneByLocation(LocationBoundaryLookup boundaryLookup)
{
    public async Task<ComunaDto> ExecuteAsync(double latitude, double longitude)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);
        return await boundaryLookup.GetCommuneAsync(latitude, longitude)
            ?? throw new NotFoundException("No se encontro una comuna para esa ubicacion.");
    }
}

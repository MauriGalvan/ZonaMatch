using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Mapping;
using ZonaMatch.Application.Policies;
using ZonaMatch.Application.Services;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetLocationSummary(
    LocationBoundaryLookup boundaryLookup,
    ISchoolRepository schoolRepository)
{
    public async Task<UbicacionResumenDto> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude, radiusMeters);

        // The repositories share a scoped DbContext, so keep these queries sequential.
        var district = await boundaryLookup.GetDistrictAsync(latitude, longitude);
        var commune = await boundaryLookup.GetCommuneAsync(latitude, longitude);
        var schools = await schoolRepository.GetNearbyByFiltersAsync(latitude, longitude, radiusMeters, null, null);

        return new UbicacionResumenDto(
            new CoordenadaDto(latitude, longitude),
            district,
            commune,
            schools.Select(school => SchoolResponseMapper.Map(school.Entity, school.DistanceMeters)).ToList());
    }
}

using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Mapping;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetNearbySchools(ISchoolRepository schoolRepository)
{
    public async Task<IReadOnlyList<SchoolResponse>> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? level,
        string? sector)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude, radiusMeters);

        var schools = await schoolRepository.GetNearbyByFiltersAsync(latitude, longitude, radiusMeters, level, sector);
        return schools.Select(school => SchoolResponseMapper.Map(school.Entity, school.DistanceMeters)).ToList();
    }
}

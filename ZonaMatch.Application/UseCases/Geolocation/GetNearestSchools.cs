using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Mapping;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetNearestSchools(ISchoolRepository schoolRepository)
{
    public async Task<IReadOnlyList<SchoolResponse>> ExecuteAsync(
        double latitude,
        double longitude,
        int quantity)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);
        GeolocationRequestValidator.ValidateQuantity(quantity);

        var schools = await schoolRepository.GetNearestAsync(latitude, longitude, quantity);
        return schools.Select(school => SchoolResponseMapper.Map(school.Entity, school.DistanceMeters)).ToList();
    }
}

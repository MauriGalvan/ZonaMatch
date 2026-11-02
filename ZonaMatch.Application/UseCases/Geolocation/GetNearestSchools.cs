using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Mapping;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetNearestSchools(ISchoolRepository schoolRepository)
{
    public async Task<IReadOnlyList<EscuelaDto>> ExecuteAsync(
        double latitude,
        double longitude,
        int quantity)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude);
        GeolocationRequestValidator.ValidateQuantity(quantity);

        var schools = await schoolRepository.GetMasCercanosAsync(latitude, longitude, quantity);
        return schools.Select(school => SchoolResponseMapper.Map(school.Entidad, school.DistanciaMetros)).ToList();
    }
}

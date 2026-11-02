using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Mapping;
using ZonaMatch.Application.Policies;

namespace ZonaMatch.Application.UseCases.Geolocation;

public sealed class GetNearbySchools(IEscuelaRepository schoolRepository)
{
    public async Task<IReadOnlyList<EscuelaDto>> ExecuteAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? level,
        string? sector)
    {
        GeolocationRequestValidator.ValidateCoordinates(latitude, longitude, radiusMeters);

        var schools = await schoolRepository.GetCercanasAsync(latitude, longitude, radiusMeters, level, sector);
        return schools.Select(school => SchoolResponseMapper.Map(school.Entidad, school.DistanciaMetros)).ToList();
    }
}

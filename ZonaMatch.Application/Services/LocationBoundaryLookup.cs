using ZonaMatch.Application.DTOs.Geolocation.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.Services;

/// <summary>
/// Shared lookup for the district and commune that contain a coordinate.
/// </summary>
public sealed class LocationBoundaryLookup(
    IDistrictRepository districtRepository,
    ICommuneRepository communeRepository)
{
    public async Task<DistrictResponse?> GetDistrictAsync(double latitude, double longitude)
    {
        var district = await districtRepository.GetContainingPointAsync(latitude, longitude);
        return district is null ? null : new DistrictResponse(district.Key, district.Nombre, district.Provincia);
    }

    public async Task<CommuneResponse?> GetCommuneAsync(double latitude, double longitude)
    {
        var commune = await communeRepository.GetContainingPointAsync(latitude, longitude);
        return commune is null ? null : new CommuneResponse(commune.Key, commune.Nombre);
    }
}

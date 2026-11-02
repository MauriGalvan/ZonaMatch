using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.Services;

/// <summary>
/// Shared lookup for the district and commune that contain a coordinate.
/// </summary>
public sealed class LocationBoundaryLookup(
    IDistrictRepository districtRepository,
    ICommuneRepository communeRepository)
{
    public async Task<PartidoDto?> GetDistrictAsync(double latitude, double longitude)
    {
        var district = await districtRepository.GetQueContienePuntoAsync(latitude, longitude);
        return district is null ? null : new PartidoDto(district.Key, district.Nombre, district.Provincia);
    }

    public async Task<ComunaDto?> GetCommuneAsync(double latitude, double longitude)
    {
        var commune = await communeRepository.GetQueContienePuntoAsync(latitude, longitude);
        return commune is null ? null : new ComunaDto(commune.Key, commune.Nombre);
    }
}

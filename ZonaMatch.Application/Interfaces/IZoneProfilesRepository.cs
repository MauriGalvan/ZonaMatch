using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Application.Interfaces
{
    // Data for all zones used by the scoring engine (census, crime, transit, amenities, and rent).
    public interface IZoneProfilesRepository
    {
        // All zones with data; the engine compares each zone with others at the same level.
        Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default);

        // Zones containing geocoded locations, contiguous zones at the same level, and zones crossed by direct
        // segments between each pair of locations (using the more detailed level of the pair).
        Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
            IReadOnlyCollection<(double Latitud, double Longitud)> ubicaciones,
            CancellationToken cancellationToken = default);
    }
}

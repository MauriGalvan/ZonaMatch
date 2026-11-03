using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal sealed class IntegrationZoneProfilesRepository : IZoneProfilesRepository
{
    private static readonly IReadOnlyList<ZoneProfile> ZoneProfiles =
    [
        new ZoneProfile
        {
            Slug = IntegrationZoneRepository.SafetyZoneSlug,
            Name = "Safety Zone",
            Level = 10,
            AreaKm2 = 5,
            Population = 1000,
            CrimeCount = 1,
            HealthCenterCount = 1,
            Latitude = -34.60,
            Longitude = -58.40
        },
        new ZoneProfile
        {
            Slug = IntegrationZoneRepository.HealthZoneSlug,
            Name = "Health Zone",
            Level = 10,
            AreaKm2 = 5,
            Population = 1000,
            CrimeCount = 9,
            HealthCenterCount = 9,
            Latitude = -34.70,
            Longitude = -58.50
        }
    ];

    public Task<IReadOnlyList<ZoneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ZoneProfiles);

    public Task<IReadOnlyList<string>> ListNearbyZoneSlugsAsync(
        IReadOnlyCollection<(double Latitude, double Longitude)> locations,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(ZoneProfiles.Select(zone => zone.Slug).ToArray());
}

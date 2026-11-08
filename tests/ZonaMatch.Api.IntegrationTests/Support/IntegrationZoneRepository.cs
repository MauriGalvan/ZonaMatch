using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal sealed class IntegrationZoneRepository : IZoneRepository
{
    public const string ZoneSlug = "integration-zone";
    public const string SafetyZoneSlug = "safety-zone";
    public const string HealthZoneSlug = "health-zone";

    private static readonly HashSet<string> ZoneSlugs =
    [
        ZoneSlug,
        SafetyZoneSlug,
        HealthZoneSlug
    ];

    public static string CreateContributionZoneSlug() => $"integration-contribution-{Guid.NewGuid():N}";
    public static string CreateCommunityZoneSlug() => $"integration-community-{Guid.NewGuid():N}";

    private static bool IsKnownZone(string slug) =>
        ZoneSlugs.Contains(slug)
        || slug.StartsWith("integration-contribution-", StringComparison.Ordinal)
        || slug.StartsWith("integration-community-", StringComparison.Ordinal);

    public Task<Zone?> GetBySlugAsync(
        string slug,
        double simplificationTolerance,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IsKnownZone(slug)
            ? new Zone
            {
                Slug = slug,
                Name = slug,
                Boundary = new ZoneBoundary(0, 0, """{"type":"Point","coordinates":[0,0]}""")
            }
            : null);

    public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(IsKnownZone(slug));

    public Task<bool> ContainsPointAsync(
        string slug,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IsKnownZone(slug));

    public Task<IReadOnlyList<Zone>> SearchAsync(
        string searchText,
        int resultLimit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Zone>>([]);
}

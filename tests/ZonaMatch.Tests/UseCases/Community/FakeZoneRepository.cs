using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Tests.UseCases.Community;

// A single zone, "villa-luro", containing every point with latitude below 0.
internal sealed class FakeZoneRepository : IZoneRepository
{
    public const string ZoneSlug = "villa-luro";

    public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        Task.FromResult(slug == ZoneSlug);

    public Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default) =>
        Task.FromResult(slug == ZoneSlug && latitude < 0);

    public Task<ZonaMatch.Domain.Entities.Zone?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ZonaMatch.Domain.Entities.Zone>> SearchAsync(string searchText, int resultLimit, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

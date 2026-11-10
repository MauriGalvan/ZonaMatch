using ZonaMatch.Application.Interfaces;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.Community;

internal sealed class FakeSavedZoneRepository : ISavedZoneRepository
{
    public List<DomainSavedZone> SavedZones { get; } = [];

    public Task<IReadOnlyList<DomainSavedZone>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<DomainSavedZone?> GetAsync(Guid userId, string zoneSlug, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DomainSavedZone>> GetForZoneAsync(
        string zoneSlug, Guid excludedUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainSavedZone>>(SavedZones
            .Where(savedZone => savedZone.ZoneSlug == zoneSlug && savedZone.UserId != excludedUserId)
            .ToList());

    public void Add(DomainSavedZone savedZone) => throw new NotSupportedException();

    public void Remove(DomainSavedZone savedZone) => throw new NotSupportedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

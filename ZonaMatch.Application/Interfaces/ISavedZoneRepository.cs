using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface ISavedZoneRepository
    {
        Task<IReadOnlyList<SavedZone>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<SavedZone?> GetAsync(Guid userId, string zoneSlug, CancellationToken cancellationToken = default);

        void Add(SavedZone savedZone);

        void Remove(SavedZone savedZone);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

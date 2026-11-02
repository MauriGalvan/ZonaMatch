using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IContributionRepository
    {
        Task<(int ApprovedCount, int PendingCount)> CountAsync(string zoneSlug, CancellationToken cancellationToken = default);

        // Returns pending contributions for a zone with their authors and votes, oldest first (read-only).
        Task<IReadOnlyList<Contribution>> ListPendingAsync(string zoneSlug, CancellationToken cancellationToken = default);

        // Returns the contribution with its votes, tracked for changes.
        Task<Contribution?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Contribution contribution);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

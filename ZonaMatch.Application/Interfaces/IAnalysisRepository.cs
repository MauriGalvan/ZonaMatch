using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Saved analyses are loaded with their criteria and zones and tracked for changes.
    // Call SaveChangesAsync to persist updates.
    public interface IAnalysisRepository
    {
        // Returns the user's active originals and versions, newest first.
        Task<IReadOnlyList<Analysis>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

        // Returns null when the analysis is not active or belongs to another user.
        Task<Analysis?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

        // Returns active versions derived from an original, oldest first.
        Task<IReadOnlyList<Analysis>> ListVersionsAsync(Guid originalAnalysisId, CancellationToken cancellationToken = default);

        // Returns the user's analyses currently within the trash retention period, newest deletion first.
        Task<IReadOnlyList<Analysis>> GetTrashedForUserAsync(
            Guid userId,
            DateTimeOffset deletedAfter,
            CancellationToken cancellationToken = default);

        // Returns every analysis in the user's trash, including items whose retention period expired.
        Task<IReadOnlyList<Analysis>> GetDeletedForUserAsync(Guid userId, CancellationToken cancellationToken = default);

        // Returns a deleted analysis owned by the user, or null when it is not in their trash.
        Task<Analysis?> GetTrashedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

        // Returns an analysis regardless of its trash state, if it belongs to the user.
        Task<Analysis?> GetIncludingDeletedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

        // Returns every version of an original, including versions already in the trash.
        Task<IReadOnlyList<Analysis>> ListVersionsIncludingDeletedAsync(
            Guid originalAnalysisId,
            CancellationToken cancellationToken = default);

        // Returns trashed analyses whose retention period has expired.
        Task<IReadOnlyList<Analysis>> GetExpiredTrashedAsync(
            DateTimeOffset deletedBefore,
            CancellationToken cancellationToken = default);

        void Add(Analysis analysis);

        // Permanently deletes the analysis together with its criteria snapshot.
        void PermanentlyDelete(Analysis analysis);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

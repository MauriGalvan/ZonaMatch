using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IReviewRepository
    {
        // Approved zone reviews plus the current user's own review whatever its status (and, for an administrator,
        // every pending review), with their authors and "useful" votes, newest first (read-only).
        Task<IReadOnlyList<Review>> ListByZoneAsync(
            string zoneSlug, Guid? currentUserId, bool includeAllPending = false, CancellationToken cancellationToken = default);

        // Reviews of every zone waiting for approval, with their authors, oldest first (read-only).
        Task<IReadOnlyList<Review>> ListPendingAsync(CancellationToken cancellationToken = default);

        // Tracked entities are persisted by the repository.
        Task<Review?> GetByUserAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default);

        // Tracked entities with their author and "useful" votes.
        Task<Review?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Review review);
        void Delete(Review review);

        // Throws ConflictException when a concurrent request already created the user's review.
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

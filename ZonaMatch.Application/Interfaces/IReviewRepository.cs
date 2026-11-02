using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    public interface IReviewRepository
    {
        // Zone reviews with their authors and "useful" votes, newest first (read-only).
        Task<IReadOnlyList<Review>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default);

        // Tracked entities are persisted by the repository.
        Task<Review?> GetByUserAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default);

        // Tracked entities with their "useful" votes.
        Task<Review?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default);

        void Add(Review review);
        void Delete(Review review);

        // Throws ConflictException when a concurrent request already created the user's review.
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}

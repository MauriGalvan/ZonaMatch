using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Community;

// Stores entities by reference, as tracked entities: SaveChangesAsync only counts calls.
internal sealed class FakeReviewRepository : IReviewRepository
{
    public List<DomainReview> Reviews { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<DomainReview>> ListByZoneAsync(
        string zoneSlug, Guid? currentUserId, bool includeAllPending = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainReview>>(
            Reviews
                .Where(r => r.ZoneSlug == zoneSlug && (r.Status == ReviewStatus.Approved || r.UserId == currentUserId
                    || (includeAllPending && r.Status == ReviewStatus.Pending)))
                .OrderByDescending(r => r.CreatedAt)
                .ToList());

    public Task<IReadOnlyList<DomainReview>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DomainReview>>(
            Reviews.Where(r => r.Status == ReviewStatus.Pending).OrderBy(r => r.UpdatedAt).ToList());

    public Task<DomainReview?> GetByUserAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reviews.FirstOrDefault(r => r.ZoneSlug == zoneSlug && r.UserId == userId));

    public Task<DomainReview?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reviews.FirstOrDefault(r => r.ZoneSlug == zoneSlug && r.Id == id));

    public void Add(DomainReview review) => Reviews.Add(review);

    public void Delete(DomainReview review) => Reviews.Remove(review);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

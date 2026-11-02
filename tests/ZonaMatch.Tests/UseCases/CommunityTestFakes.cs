using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases
{
    // In-memory repositories for community use cases. Entities are stored by reference,
    // as tracked entities: SaveChangesAsync only counts calls.

    internal sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }

    // A single zone, "villa-luro", containing every point with latitude below 0.
    internal sealed class FakeZoneRepository : IZoneRepository
    {
        public const string ZoneSlug = "villa-luro";

        public Task<bool> ExistsAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult(slug == ZoneSlug);

        public Task<bool> ContainsPointAsync(string slug, double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult(slug == ZoneSlug && latitude < 0);

        public Task<string?> GetBySlugAsync(string slug, double simplificationTolerance, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ZoneSearchResultResponse>> SearchAsync(string searchFragment, int resultLimit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    internal sealed class FakeContributionRepository : IContributionRepository
    {
        public List<Contribution> Contributions { get; } = [];
        public int SaveCount { get; private set; }

        public Task<(int ApprovedCount, int PendingCount)> CountAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult((
                Contributions.Count(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Approved),
                Contributions.Count(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Pending)));

        public Task<IReadOnlyList<Contribution>> ListPendingAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Contribution>>(
                Contributions.Where(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Pending).ToList());

        public Task<Contribution?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Contributions.FirstOrDefault(a => a.ZoneSlug == zoneSlug && a.Id == id));

        public void Add(Contribution contribution) => Contributions.Add(contribution);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeCommunityPoiRepository : ICommunityPoiRepository
    {
        public List<CommunityPoi> Pois { get; } = [];

        public Task<CommunityPoi?> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Pois.FirstOrDefault(p => p.Id == id));

        public void Add(CommunityPoi poi) => Pois.Add(poi);
    }

    internal sealed class FakeReviewRepository : IReviewRepository
    {
        public List<Review> Reviews { get; } = [];
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<Review>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Review>>(
                Reviews.Where(r => r.ZoneSlug == zoneSlug).OrderByDescending(r => r.CreatedAt).ToList());

        public Task<Review?> GetByUserAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Reviews.FirstOrDefault(r => r.ZoneSlug == zoneSlug && r.UserId == userId));

        public Task<Review?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Reviews.FirstOrDefault(r => r.ZoneSlug == zoneSlug && r.Id == id));

        public void Add(Review review) => Reviews.Add(review);

        public void Delete(Review review) => Reviews.Remove(review);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeQuestionRepository : IQuestionRepository
    {
        public List<Question> Questions { get; } = [];

        public Task<IReadOnlyList<Question>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Question>>(
                Questions.Where(p => p.ZoneSlug == zoneSlug).OrderByDescending(p => p.CreatedAt).ToList());

        public Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Questions.Any(p => p.ZoneSlug == zoneSlug && p.Id == id));

        public Task<Question?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Questions.FirstOrDefault(p => p.ZoneSlug == zoneSlug && p.Id == id));

        public void Add(Question question) => Questions.Add(question);

        public void Add(Answer answer) =>
            Questions.Single(question => question.Id == answer.QuestionId).Answers.Add(answer);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeSavedZoneRepository : ISavedZoneRepository
    {
        public Task<IReadOnlyList<SavedZone>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SavedZone?> GetAsync(Guid userId, string zoneSlug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SavedZone>> GetForZoneAsync(
            string zoneSlug, Guid excludedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SavedZone>>([]);

        public void Add(SavedZone savedZone) => throw new NotSupportedException();

        public void Remove(SavedZone savedZone) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<UserNotification> Notifications { get; } = [];

        public Task<IReadOnlyList<UserNotification>> GetForUserAsync(
            Guid userId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserNotification?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void AddRange(IEnumerable<UserNotification> notifications) => Notifications.AddRange(notifications);

        public Task MarkAllAsReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

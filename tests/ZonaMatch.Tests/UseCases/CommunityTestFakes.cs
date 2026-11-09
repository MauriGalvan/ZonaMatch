using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Zone.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using DomainContribution = ZonaMatch.Domain.Entities.Contribution;
using DomainQuestion = ZonaMatch.Domain.Entities.Question;
using DomainReview = ZonaMatch.Domain.Entities.Review;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

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
        public List<DomainContribution> Contributions { get; } = [];
        public int SaveCount { get; private set; }

        public Task<(int ApprovedCount, int PendingCount)> CountAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult((
                Contributions.Count(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Approved),
                Contributions.Count(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Pending)));

        public Task<IReadOnlyList<DomainContribution>> ListPendingAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DomainContribution>>(
                Contributions.Where(a => a.ZoneSlug == zoneSlug && a.Status == ContributionStatus.Pending).ToList());

        public Task<DomainContribution?> GetAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Contributions.FirstOrDefault(a => a.ZoneSlug == zoneSlug && a.Id == id));

        public void Add(DomainContribution contribution) => Contributions.Add(contribution);

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

    internal sealed class FakeQuestionRepository : IQuestionRepository
    {
        public List<DomainQuestion> Questions { get; } = [];

        public Task<IReadOnlyList<DomainQuestion>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DomainQuestion>>(
                Questions.Where(p => p.ZoneSlug == zoneSlug).OrderByDescending(p => p.CreatedAt).ToList());

        public Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Questions.Any(p => p.ZoneSlug == zoneSlug && p.Id == id));

        public Task<DomainQuestion?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Questions.FirstOrDefault(p => p.ZoneSlug == zoneSlug && p.Id == id));

        public void Add(DomainQuestion question) => Questions.Add(question);

        public void Add(Answer answer) =>
            Questions.Single(question => question.Id == answer.QuestionId).Answers.Add(answer);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

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

        public List<Guid> ReadModerationReviewIds { get; } = [];

        public Task MarkReviewModerationAsReadAsync(Guid reviewId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
        {
            ReadModerationReviewIds.Add(reviewId);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed record CapturedEmail(string To, string Subject, string Body);

    // Records every email; recipients listed in FailingRecipients fail as if the SMTP server rejected them.
    internal sealed class CapturingEmailService : IEmailService
    {
        public List<CapturedEmail> Sent { get; } = [];
        public List<string> FailingRecipients { get; } = [];

        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FailingRecipients.Contains(to))
                return Task.FromException(new InvalidOperationException("The SMTP server is not available."));

            Sent.Add(new CapturedEmail(to, subject, body));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
            SendEmailAsync(to, subject, body, cancellationToken);
    }

    // Only the administrators lookup is used by the community use cases.
    internal sealed class FakeAdministratorRepository : IUserRepository
    {
        public List<string> AdministratorEmails { get; } = [];
        public List<Guid> AdministratorIds { get; } = [];
        public bool FailLookup { get; set; }

        public Task<IReadOnlyList<string>> GetAdministratorEmailsAsync(CancellationToken cancellationToken = default) =>
            FailLookup
                ? Task.FromException<IReadOnlyList<string>>(new InvalidOperationException("The database is not available."))
                : Task.FromResult<IReadOnlyList<string>>(AdministratorEmails.ToList());

        public Task<IReadOnlyList<Guid>> GetAdministratorIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(AdministratorIds.ToList());

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

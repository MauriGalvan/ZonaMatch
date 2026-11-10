using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Tests.UseCases.Community;
using DomainReview = ZonaMatch.Domain.Entities.Review;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class ResolveReviewTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly FakeSavedZoneRepository _savedZones = new();
        private readonly FakeNotificationRepository _notifications = new();
        private readonly CapturingEmailService _emails = new();
        private readonly ResolveReview _useCase;
        private readonly Guid _administrator = Guid.NewGuid();
        private readonly DomainReview _review;

        public ResolveReviewTests()
        {
            _useCase = new ResolveReview(
                _reviews,
                _savedZones,
                _notifications,
                new ReviewModerationEmails(
                    new FakeAdministratorRepository(),
                    _emails,
                    new FrontendLinks("http://localhost:5173/"),
                    NullLogger<ReviewModerationEmails>.Instance),
                new FixedTimeProvider(Now));
            _review = new DomainReview
            {
                ZoneSlug = Zone,
                UserId = Guid.NewGuid(),
                User = new User { Email = "ana@example.com" },
                CreatedAt = Now.AddDays(-1)
            };
            _reviews.Add(_review);
        }

        [Fact]
        public async Task Execute_WhenApproving_PublishesTheReview()
        {
            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: true, reason: null);

            Assert.Equal(ReviewStatus.Approved, _review.Status);
            Assert.Equal(_administrator, _review.ResolvedByUserId);
            Assert.Equal(Now, _review.ResolvedAt);
            Assert.Equal(Now, _review.PublishedAt);
            Assert.Null(_review.RejectionReason);
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Execute_ClearsTheModerationNotificationsOfTheReview(bool approve)
        {
            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve, reason: approve ? null : "Incluye datos personales.");

            Assert.Equal(_review.Id, Assert.Single(_notifications.ReadModerationReviewIds));
        }

        [Fact]
        public async Task Execute_WhenApprovingTheFirstTime_NotifiesOtherUsersWhoSavedTheZone()
        {
            var savedZoneUserId = Guid.NewGuid();
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = savedZoneUserId, ZoneSlug = Zone, Name = "Villa Luro" });
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = _review.UserId, ZoneSlug = Zone, Name = "Villa Luro" });
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = Guid.NewGuid(), ZoneSlug = "otra-zona", Name = "Otra zona" });

            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: true, reason: null);

            var notification = Assert.Single(_notifications.Notifications);
            Assert.Equal(savedZoneUserId, notification.UserId);
            Assert.Equal(NotificationType.SavedZoneReview, notification.Type);
            Assert.Equal($"/zonas/{Zone}?tab=comunidad", notification.Link);
            Assert.Equal(Now, notification.CreatedAt);
        }

        [Fact]
        public async Task Execute_WhenApprovingAnEditedReviewThatWasAlreadyPublished_DoesNotNotifyAgain()
        {
            var firstPublication = Now.AddHours(-5);
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = Guid.NewGuid(), ZoneSlug = Zone, Name = "Villa Luro" });
            _review.Resolve(_administrator, approve: true, rejectionReason: null, firstPublication);
            _review.ReturnToPending();

            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: true, reason: null);

            Assert.Equal(ReviewStatus.Approved, _review.Status);
            Assert.Equal(firstPublication, _review.PublishedAt);
            Assert.Equal(Now, _review.ResolvedAt);
            Assert.Empty(_notifications.Notifications);
        }

        [Fact]
        public async Task Execute_WhenRejecting_StoresTheTrimmedReasonAndDoesNotPublishOrNotify()
        {
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = Guid.NewGuid(), ZoneSlug = Zone, Name = "Villa Luro" });

            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: false, reason: "  El texto incluye datos personales.  ");

            Assert.Equal(ReviewStatus.Rejected, _review.Status);
            Assert.Equal("El texto incluye datos personales.", _review.RejectionReason);
            Assert.Equal(_administrator, _review.ResolvedByUserId);
            Assert.Null(_review.PublishedAt);
            Assert.Empty(_notifications.Notifications);
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Execute_WhenRejectingWithoutAReason_ThrowsBadRequestAndKeepsItPending(string? reason)
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: false, reason));

            Assert.Equal(ReviewStatus.Pending, _review.Status);
            Assert.Equal(0, _reviews.SaveCount);
        }

        [Theory]
        [InlineData(ReviewStatus.Approved)]
        [InlineData(ReviewStatus.Rejected)]
        public async Task Execute_WhenTheReviewWasAlreadyResolved_ThrowsConflict(ReviewStatus status)
        {
            _review.Status = status;

            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: true, reason: null));

            Assert.Equal(status, _review.Status);
            Assert.Equal(0, _reviews.SaveCount);
        }

        [Fact]
        public async Task Execute_WhenApproving_EmailsTheAuthorThatTheReviewIsPublic()
        {
            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: true, reason: null);

            var email = Assert.Single(_emails.Sent);
            Assert.Equal("ana@example.com", email.To);
            Assert.Equal("Tu reseña en ZonaMatch fue aprobada", email.Subject);
            Assert.Contains("Tu reseña ya es pública", email.Body);
            Assert.Contains($"http://localhost:5173/zonas/{Zone}?tab=comunidad", email.Body);
        }

        [Fact]
        public async Task Execute_WhenRejecting_EmailsTheAuthorTheReason()
        {
            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: false, reason: "Incluye <datos> personales.");

            var email = Assert.Single(_emails.Sent);
            Assert.Equal("ana@example.com", email.To);
            Assert.Equal("Tu reseña en ZonaMatch fue rechazada", email.Subject);
            Assert.Contains("<strong>Motivo:</strong> Incluye &lt;datos&gt; personales.", email.Body);
        }

        [Fact]
        public async Task Execute_WhenTheEmailFails_StillResolvesTheReview()
        {
            _emails.FailingRecipients.Add("ana@example.com");

            await _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: true, reason: null);

            Assert.Equal(ReviewStatus.Approved, _review.Status);
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Fact]
        public async Task Execute_WhenTheResolutionIsInvalid_DoesNotEmailTheAuthor()
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => _useCase.ExecuteAsync(Zone, _review.Id, _administrator, approve: false, reason: " "));

            Assert.Empty(_emails.Sent);
        }

        [Fact]
        public async Task Execute_WhenTheReviewDoesNotExistInTheZone_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), _administrator, approve: true, reason: null));
            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync("otra-zona", _review.Id, _administrator, approve: true, reason: null));

            Assert.Equal(ReviewStatus.Pending, _review.Status);
        }
    }
}

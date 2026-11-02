using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Enums;
using DomainSavedZone = ZonaMatch.Domain.Entities.SavedZone;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class SaveMyReviewTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly FakeSavedZoneRepository _savedZones = new();
        private readonly FakeNotificationRepository _notifications = new();
        private readonly SaveMyReview _useCase;
        private readonly AuthenticatedUserContext _ana = new(Guid.NewGuid(), "ana@example.com");

        public SaveMyReviewTests()
        {
            _useCase = new SaveMyReview(
                _reviews,
                new FakeZoneRepository(),
                _savedZones,
                _notifications,
                new FixedTimeProvider(CurrentTime));
        }

        private static SaveReviewRequest Review(int safety = 3, string[]? topics = null) =>
            new(new ReviewAspectScoresRequest(safety, 5, 4, 4, 2), "  Barrio tranquilo y bien conectado con el centro.  ", 9, topics);

        [Fact]
        public async Task Save_Twice_ReplacesTheUsersReview()
        {
            await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 3));
            var second = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 1, topics: ["Transporte", "seguridad"]));

            var savedReview = Assert.Single(_reviews.Reviews);
            Assert.Equal(1, savedReview.SafetyScore);
            Assert.Equal(5, savedReview.TransitScore);
            Assert.Equal("Barrio tranquilo y bien conectado con el centro.", savedReview.Text);
            Assert.Equal([ReviewAspect.Safety, ReviewAspect.PublicTransport], savedReview.Topics);
            Assert.Equal(CurrentTime, savedReview.CreatedAt);
            Assert.True(second.IsMine);
            Assert.Equal("AN", second.Initials);
        }

        [Fact]
        public async Task Save_WhenCreatingReview_NotifiesOtherUsersWhoSavedTheZoneOnlyOnce()
        {
            var savedZoneUserId = Guid.NewGuid();
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = savedZoneUserId, ZoneSlug = Zone, Name = "Villa Luro" });
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = _ana.Id, ZoneSlug = Zone, Name = "Villa Luro" });
            _savedZones.SavedZones.Add(new DomainSavedZone { UserId = Guid.NewGuid(), ZoneSlug = "otra-zona", Name = "Otra zona" });

            await _useCase.ExecuteAsync(Zone, _ana, Review());
            await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 4));

            var notification = Assert.Single(_notifications.Notifications);
            Assert.Equal(savedZoneUserId, notification.UserId);
            Assert.Equal(NotificationType.SavedZoneReview, notification.Type);
            Assert.Equal($"/zonas/{Zone}?tab=comunidad", notification.Link);
            Assert.Single(_reviews.Reviews);
        }

        [Fact]
        public async Task Save_ReturnsTheOverallScoreCalculatedFromTheAspects()
        {
            var savedReview = await _useCase.ExecuteAsync(Zone, _ana, Review(safety: 3));

            Assert.Equal(3.6, savedReview.Rating);
        }

        [Fact]
        public async Task Save_WithUnknownTopic_ThrowsBadRequest()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Zone, _ana, Review(topics: ["ruido"])));
            Assert.Empty(_reviews.Reviews);
        }

        [Fact]
        public async Task Save_WhenZoneDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync("no-existe", _ana, Review()));
            Assert.Empty(_reviews.Reviews);
        }
    }
}

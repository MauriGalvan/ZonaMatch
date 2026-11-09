using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Entities;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class DeleteMyReviewTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;

        private readonly FakeReviewRepository _reviews = new();
        private readonly FakeNotificationRepository _notifications = new();
        private readonly DeleteMyReview _useCase;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();

        public DeleteMyReviewTests()
        {
            _useCase = new DeleteMyReview(_reviews, _notifications, new FixedTimeProvider(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public async Task Delete_ClearsTheModerationNotificationsOfTheDeletedReview()
        {
            var review = new DomainReview { ZoneSlug = Zone, UserId = _ana };
            _reviews.Add(review);

            await _useCase.ExecuteAsync(Zone, _ana);

            Assert.Equal(review.Id, Assert.Single(_notifications.ReadModerationReviewIds));
        }

        [Fact]
        public async Task Delete_RemovesOnlyTheCurrentUsersReview()
        {
            _reviews.Add(new DomainReview { ZoneSlug = Zone, UserId = _ana });
            var luisReview = new DomainReview { ZoneSlug = Zone, UserId = _luis };
            _reviews.Add(luisReview);

            await _useCase.ExecuteAsync(Zone, _ana);

            Assert.Same(luisReview, Assert.Single(_reviews.Reviews));
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Fact]
        public async Task Delete_WhenReviewDoesNotExist_ThrowsNotFound()
        {
            _reviews.Add(new DomainReview { ZoneSlug = Zone, UserId = _luis });

            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, _ana));
            Assert.Single(_reviews.Reviews);
        }
    }
}

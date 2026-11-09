using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Entities;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class SetReviewHelpfulTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly SetReviewHelpful _useCase;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();
        private readonly DomainReview _anaReview;

        public SetReviewHelpfulTests()
        {
            _useCase = new SetReviewHelpful(_reviews, new FixedTimeProvider(CurrentTime));
            _anaReview = new DomainReview { ZoneSlug = Zone, UserId = _ana, Status = ReviewStatus.Approved, CreatedAt = CurrentTime };
            _reviews.Add(_anaReview);
        }

        [Fact]
        public async Task MarkHelpful_Twice_SavesOnlyOneVote()
        {
            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: true);
            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: true);

            var vote = Assert.Single(_anaReview.HelpfulVotes);
            Assert.Equal(_luis, vote.UserId);
            Assert.Equal(CurrentTime, vote.CreatedAt);
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Fact]
        public async Task UnmarkHelpful_RemovesVoteAndIsIdempotent()
        {
            _anaReview.HelpfulVotes.Add(new ReviewHelpfulVote { ReviewId = _anaReview.Id, UserId = _luis, CreatedAt = CurrentTime });

            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: false);
            await _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: false);

            Assert.Empty(_anaReview.HelpfulVotes);
            Assert.Equal(1, _reviews.SaveCount);
        }

        [Fact]
        public async Task MarkHelpful_OnOwnReview_IsRejected()
        {
            await Assert.ThrowsAsync<ConflictException>(
                () => _useCase.ExecuteAsync(Zone, _anaReview.Id, _ana, helpful: true));
            Assert.Empty(_anaReview.HelpfulVotes);
        }

        [Theory]
        [InlineData(ReviewStatus.Pending)]
        [InlineData(ReviewStatus.Rejected)]
        public async Task MarkHelpful_OnAReviewThatIsNotApproved_ThrowsNotFound(ReviewStatus status)
        {
            _anaReview.Status = status;

            await Assert.ThrowsAsync<NotFoundException>(
                () => _useCase.ExecuteAsync(Zone, _anaReview.Id, _luis, helpful: true));
            Assert.Empty(_anaReview.HelpfulVotes);
            Assert.Equal(0, _reviews.SaveCount);
        }

        [Fact]
        public async Task MarkHelpful_WhenReviewDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), _luis, helpful: true));
        }
    }
}

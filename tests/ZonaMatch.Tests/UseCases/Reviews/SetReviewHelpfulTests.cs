using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Reviews
{
    public class SetReviewHelpfulTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly SetReviewHelpful _useCase;
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _luis = Guid.NewGuid();
        private readonly Review _anaReview;

        public SetReviewHelpfulTests()
        {
            _useCase = new SetReviewHelpful(_reviews, new FixedTimeProvider(CurrentTime));
            _anaReview = new Review { ZoneSlug = Zone, UserId = _ana, CreatedAt = CurrentTime };
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

        [Fact]
        public async Task MarkHelpful_WhenReviewDoesNotExist_ThrowsNotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _useCase.ExecuteAsync(Zone, Guid.NewGuid(), _luis, helpful: true));
        }
    }
}

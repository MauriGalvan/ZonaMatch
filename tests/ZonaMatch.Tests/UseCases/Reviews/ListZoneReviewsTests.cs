using ZonaMatch.Application.DTOs.Reviews.Responses;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.Reviews
{
    public class ListZoneReviewsTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly ListZoneReviews _useCase;
        private readonly User _ana = new() { Id = Guid.NewGuid(), Email = "ana@example.com" };
        private readonly Guid _luis = Guid.NewGuid();

        public ListZoneReviewsTests()
        {
            _useCase = new ListZoneReviews(_reviews);
        }

        // With a safety score of 3, the overall score is (3 + 5 + 4 + 4 + 2) / 5 = 3.6.
        private Review Add(User author, int safety = 3, string zone = Zone)
        {
            var resena = new Review
            {
                ZoneSlug = zone,
                UserId = author.Id,
                User = author,
                SafetyScore = safety,
                TransitScore = 5,
                ConnectivityScore = 4,
                CommerceScore = 4,
                GreenSpaceScore = 2,
                Text = "Barrio tranquilo y bien conectado con el centro.",
                CreatedAt = CurrentTime
            };
            _reviews.Add(resena);
            return resena;
        }

        [Fact]
        public async Task List_SummarizesAverageScoresByAspect()
        {
            Add(_ana, safety: 3);
            Add(new User { Id = _luis, Email = "luis@example.com" }, safety: 4);
            Add(new User { Email = "mara@example.com" }, safety: 4);
            Add(_ana, safety: 1, zone: "otra-zona");

            var result = await _useCase.ExecuteAsync(Zone, currentUserId: null);

            // Overall scores 3.6 + 3.8 + 3.8 = 11.2 / 3 = 3.73...
            Assert.Equal(3.7, result.Summary.Average);
            Assert.Equal(3, result.Summary.Total);
            Assert.Equal(new ReviewAspectRatingResponse(ReviewAspect.Safety, 3.7), result.Summary.Aspects[0]);
            Assert.Equal([3.6, 3.8, 3.8], result.Reviews.Select(review => review.Rating).Order());
            Assert.All(result.Reviews, review => Assert.False(review.IsMine));
        }

        [Fact]
        public async Task List_WhenThereAreNoReviews_ReturnsZeroAverage()
        {
            var result = await _useCase.ExecuteAsync(Zone, currentUserId: null);

            Assert.Equal(0, result.Summary.Average);
            Assert.Equal(ReviewAspect.All.Count, result.Summary.Aspects.Count);
            Assert.Empty(result.Reviews);
        }

        [Fact]
        public async Task List_WithCurrentUser_IndicatesOwnershipAndHelpfulVotes()
        {
            var review = Add(_ana);
            review.HelpfulVotes.Add(new ReviewHelpfulVote { ReviewId = review.Id, UserId = _luis, CreatedAt = CurrentTime });

            var forLuis = Assert.Single((await _useCase.ExecuteAsync(Zone, _luis)).Reviews);
            var forAna = Assert.Single((await _useCase.ExecuteAsync(Zone, _ana.Id)).Reviews);

            Assert.Equal(1, forLuis.HelpfulCount);
            Assert.True(forLuis.MarkedHelpfulByMe);
            Assert.False(forLuis.IsMine);
            Assert.False(forAna.MarkedHelpfulByMe);
            Assert.True(forAna.IsMine);
            Assert.Equal("AN", forAna.Initials);
        }
    }
}

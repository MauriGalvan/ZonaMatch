using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Image;
using ZonaMatch.Tests.UseCases.Community;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class ListZoneReviewsTests
    {
        private const string Zone = FakeZoneRepository.ZoneSlug;
        private static readonly DateTimeOffset CurrentTime = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly ListZoneReviews _useCase;
        private readonly User _firstUser = new() { Id = Guid.NewGuid(), Email = "ana@example.com" };
        private readonly Guid _secondUserId = Guid.NewGuid();

        public ListZoneReviewsTests()
        {
            _useCase = new ListZoneReviews(_reviews, new FakeImageStorage());
        }

        // With a safety score of 3, the overall score is (3 + 5 + 4 + 4 + 2) / 5 = 3.6.
        // Reviews are approved by default so each test only declares the ones that are not.
        private DomainReview Add(User author, int safety = 3, string zone = Zone, ReviewStatus status = ReviewStatus.Approved)
        {
            var review = new DomainReview
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
                Status = status,
                CreatedAt = CurrentTime
            };
            _reviews.Add(review);
            return review;
        }

        [Fact]
        public async Task List_IncludesTheUrlOfEachImage()
        {
            var imageName = new string('a', 32) + ".jpg";
            Add(_firstUser).ImageNames = [imageName];

            var result = await _useCase.ExecuteAsync(Zone, currentUserId: null);

            var image = Assert.Single(Assert.Single(result.Reviews).Images);
            Assert.Equal(imageName, image.Name);
            Assert.Equal($"https://images.test/{imageName}", image.Url);
        }

        [Fact]
        public async Task List_SummarizesAverageScoresByAspect()
        {
            Add(_firstUser, safety: 3);
            Add(new User { Id = _secondUserId, Email = "luis@example.com" }, safety: 4);
            Add(new User { Email = "mara@example.com" }, safety: 4);
            Add(_firstUser, safety: 1, zone: "otra-zona");

            var result = await _useCase.ExecuteAsync(Zone, currentUserId: null);

            // Overall scores 3.6 + 3.8 + 3.8 = 11.2 / 3 = 3.73...
            Assert.Equal(3.7, result.Summary.Average);
            Assert.Equal(3, result.Summary.Total);
            Assert.Equal(new ReviewAspectRatingResponse(ReviewAspect.Safety, 3.7), result.Summary.Aspects[0]);
            Assert.Equal([3.6, 3.8, 3.8], result.Reviews.Select(review => review.Rating).Order());
            Assert.All(result.Reviews, review => Assert.False(review.IsMine));
        }

        [Fact]
        public async Task List_HidesPendingAndRejectedReviewsFromOtherUsers()
        {
            Add(_firstUser);
            Add(new User { Id = _secondUserId, Email = "luis@example.com" }, status: ReviewStatus.Pending);
            Add(new User { Email = "mara@example.com" }, status: ReviewStatus.Rejected);

            var anonymous = await _useCase.ExecuteAsync(Zone, currentUserId: null);
            var forAnotherUser = await _useCase.ExecuteAsync(Zone, Guid.NewGuid());

            Assert.Single(anonymous.Reviews);
            Assert.Single(forAnotherUser.Reviews);
            Assert.All(anonymous.Reviews.Concat(forAnotherUser.Reviews), review => Assert.Equal(ReviewStatus.Approved, review.Status));
        }

        [Fact]
        public async Task List_ShowsThePendingReviewToItsAuthorWithoutCountingItInTheSummary()
        {
            Add(_firstUser, safety: 3);
            Add(new User { Id = _secondUserId, Email = "luis@example.com" }, safety: 1, status: ReviewStatus.Pending);

            var forAuthor = await _useCase.ExecuteAsync(Zone, _secondUserId);
            var forAnotherUser = await _useCase.ExecuteAsync(Zone, currentUserId: null);

            Assert.Equal(2, forAuthor.Reviews.Count);
            var own = Assert.Single(forAuthor.Reviews, review => review.IsMine);
            Assert.Equal(ReviewStatus.Pending, own.Status);
            Assert.Equal(1, forAuthor.Summary.Total);
            Assert.Equal(3.6, forAuthor.Summary.Average);
            Assert.Equal(forAnotherUser.Summary.Total, forAuthor.Summary.Total);
            Assert.Equal(forAnotherUser.Summary.Average, forAuthor.Summary.Average);
        }

        [Fact]
        public async Task List_ShowsTheRejectionReasonOnlyToTheAuthor()
        {
            var rejected = Add(_firstUser, status: ReviewStatus.Rejected);
            rejected.RejectionReason = "El texto incluye datos personales.";

            var forAuthor = Assert.Single((await _useCase.ExecuteAsync(Zone, _firstUser.Id)).Reviews);
            var forAnotherUser = await _useCase.ExecuteAsync(Zone, _secondUserId);

            Assert.Equal(ReviewStatus.Rejected, forAuthor.Status);
            Assert.Equal("El texto incluye datos personales.", forAuthor.RejectionReason);
            Assert.Empty(forAnotherUser.Reviews);
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
        public async Task List_ForAnAdministrator_IncludesPendingReviewsOfOtherUsersButNotRejectedOnes()
        {
            var pending = Add(_firstUser, status: ReviewStatus.Pending);
            Add(new User { Id = Guid.NewGuid(), Email = "leo@example.com" }, status: ReviewStatus.Rejected);

            var forAdministrator = await _useCase.ExecuteAsync(Zone, _secondUserId, isAdministrator: true);
            var forOtherUser = await _useCase.ExecuteAsync(Zone, _secondUserId);

            Assert.Equal(pending.Id, Assert.Single(forAdministrator.Reviews).Id);
            Assert.Equal(0, forAdministrator.Summary.Total);
            Assert.Empty(forOtherUser.Reviews);
        }

        [Fact]
        public async Task List_WithCurrentUser_IndicatesOwnershipAndHelpfulVotes()
        {
            var review = Add(_firstUser);
            review.HelpfulVotes.Add(new ReviewHelpfulVote { ReviewId = review.Id, UserId = _secondUserId, CreatedAt = CurrentTime });

            var forSecondUser = Assert.Single((await _useCase.ExecuteAsync(Zone, _secondUserId)).Reviews);
            var forFirstUser = Assert.Single((await _useCase.ExecuteAsync(Zone, _firstUser.Id)).Reviews);

            Assert.Equal(1, forSecondUser.HelpfulCount);
            Assert.True(forSecondUser.MarkedHelpfulByMe);
            Assert.False(forSecondUser.IsMine);
            Assert.False(forFirstUser.MarkedHelpfulByMe);
            Assert.True(forFirstUser.IsMine);
            Assert.Equal("AN", forFirstUser.Initials);
        }
    }
}

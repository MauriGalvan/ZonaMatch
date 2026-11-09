using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Tests.UseCases.Image;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Tests.UseCases.Review
{
    public class ListPendingReviewsTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

        private readonly FakeReviewRepository _reviews = new();
        private readonly ListPendingReviews _useCase;

        public ListPendingReviewsTests()
        {
            _useCase = new ListPendingReviews(_reviews, new FakeImageStorage());
        }

        private DomainReview Add(string zone, ReviewStatus status, DateTimeOffset updatedAt)
        {
            var review = new DomainReview
            {
                ZoneSlug = zone,
                UserId = Guid.NewGuid(),
                User = new User { Email = "ana@example.com" },
                Text = "Barrio tranquilo y bien conectado con el centro.",
                Status = status,
                CreatedAt = updatedAt,
                UpdatedAt = updatedAt
            };
            _reviews.Add(review);
            return review;
        }

        [Fact]
        public async Task Execute_ReturnsOnlyPendingReviewsOfEveryZoneOldestFirst()
        {
            var newer = Add("villa-luro", ReviewStatus.Pending, Now);
            var older = Add("otra-zona", ReviewStatus.Pending, Now.AddHours(-2));
            Add("villa-luro", ReviewStatus.Approved, Now.AddHours(-3));
            Add("villa-luro", ReviewStatus.Rejected, Now.AddHours(-4));

            var result = await _useCase.ExecuteAsync();

            Assert.Equal([older.Id, newer.Id], result.Select(item => item.Review.Id));
            Assert.Equal(["otra-zona", "villa-luro"], result.Select(item => item.ZoneSlug));
            Assert.All(result, item => Assert.Equal(ReviewStatus.Pending, item.Review.Status));
        }

        [Fact]
        public async Task Execute_IncludesTheUrlOfEachImageForTheAdministrator()
        {
            var imageName = new string('a', 32) + ".jpg";
            Add("villa-luro", ReviewStatus.Pending, Now).ImageNames = [imageName];

            var review = Assert.Single(await _useCase.ExecuteAsync()).Review;

            var image = Assert.Single(review.Images);
            Assert.Equal($"https://images.test/{imageName}", image.Url);
            Assert.Equal("AN", review.Initials);
        }

        [Fact]
        public async Task Execute_WhenNothingIsPending_ReturnsAnEmptyList()
        {
            Add("villa-luro", ReviewStatus.Approved, Now);

            Assert.Empty(await _useCase.ExecuteAsync());
        }
    }
}

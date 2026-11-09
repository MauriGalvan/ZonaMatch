using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ReviewTests
    {
        [Theory]
        [InlineData(3, 5, 4, 4, 2, 3.6)]
        [InlineData(5, 5, 5, 5, 5, 5.0)]
        [InlineData(1, 1, 1, 1, 2, 1.2)]
        public void OverallScore_WhenReviewIsCreated_IsAverageOfAllAspects(
            int safety, int transit, int connectivity, int commerce, int greenSpaces, double expected)
        {
            var review = new Review
            {
                SafetyScore = safety,
                TransitScore = transit,
                ConnectivityScore = connectivity,
                CommerceScore = commerce,
                GreenSpaceScore = greenSpaces
            };

            review.OverallScore.Should().Be(expected);
        }

        [Fact]
        public void OverallScore_WhenAnAspectIsEdited_RecalculatesAverage()
        {
            var review = new Review
            {
                SafetyScore = 4,
                TransitScore = 4,
                ConnectivityScore = 4,
                CommerceScore = 4,
                GreenSpaceScore = 4
            };

            review.SafetyScore = 1;

            review.OverallScore.Should().Be(3.4);
        }

        [Fact]
        public void Status_WhenReviewIsCreated_IsPending()
        {
            var review = new Review();

            review.Status.Should().Be(ReviewStatus.Pending);
            review.PublishedAt.Should().BeNull();
        }

        [Fact]
        public void Resolve_WhenApproving_PublishesTheReview()
        {
            var administrator = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
            var review = new Review { RejectionReason = "Texto viejo." };

            review.Resolve(administrator, approve: true, rejectionReason: "ignorado", now);

            review.Status.Should().Be(ReviewStatus.Approved);
            review.RejectionReason.Should().BeNull();
            review.ResolvedByUserId.Should().Be(administrator);
            review.ResolvedAt.Should().Be(now);
            review.PublishedAt.Should().Be(now);
        }

        [Fact]
        public void Resolve_WhenRejecting_StoresTheReasonWithoutPublishing()
        {
            var review = new Review();

            review.Resolve(Guid.NewGuid(), approve: false, "Incluye datos personales.", DateTimeOffset.UnixEpoch);

            review.Status.Should().Be(ReviewStatus.Rejected);
            review.RejectionReason.Should().Be("Incluye datos personales.");
            review.PublishedAt.Should().BeNull();
        }

        [Fact]
        public void Resolve_WhenApprovingAnEditedReview_KeepsTheFirstPublicationDate()
        {
            var firstPublication = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
            var review = new Review();
            review.Resolve(Guid.NewGuid(), approve: true, rejectionReason: null, firstPublication);
            review.ReturnToPending();

            review.Resolve(Guid.NewGuid(), approve: true, rejectionReason: null, firstPublication.AddDays(3));

            review.PublishedAt.Should().Be(firstPublication);
            review.ResolvedAt.Should().Be(firstPublication.AddDays(3));
        }

        [Fact]
        public void ReturnToPending_ClearsTheResolutionButKeepsThePublicationDate()
        {
            var publication = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
            var review = new Review();
            review.Resolve(Guid.NewGuid(), approve: true, rejectionReason: null, publication);

            review.ReturnToPending();

            review.Status.Should().Be(ReviewStatus.Pending);
            review.ResolvedAt.Should().BeNull();
            review.ResolvedByUserId.Should().BeNull();
            review.RejectionReason.Should().BeNull();
            review.PublishedAt.Should().Be(publication);
        }
    }
}

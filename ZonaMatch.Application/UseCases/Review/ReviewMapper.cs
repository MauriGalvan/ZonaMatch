using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Application.UseCases.Review
{
    // Maps domain reviews to API responses. The current user is null for anonymous requests.
    internal static class ReviewMapper
    {
        public static ReviewSummaryResponse Summarize(IReadOnlyList<DomainReview> reviews)
        {
            static double Average(IReadOnlyList<DomainReview> items, Func<DomainReview, double> score) =>
                items.Count == 0 ? 0 : Math.Round(items.Average(score), 1);

            return new ReviewSummaryResponse(
                Average(reviews, review => review.OverallScore),
                reviews.Count,
                reviews.Count(review => review.IsVerified),
                [
                    new ReviewAspectRatingResponse(ReviewAspect.Safety, Average(reviews, review => review.SafetyScore)),
                    new ReviewAspectRatingResponse(ReviewAspect.PublicTransport, Average(reviews, review => review.TransitScore)),
                    new ReviewAspectRatingResponse(ReviewAspect.Connectivity, Average(reviews, review => review.ConnectivityScore)),
                    new ReviewAspectRatingResponse(ReviewAspect.Shopping, Average(reviews, review => review.CommerceScore)),
                    new ReviewAspectRatingResponse(ReviewAspect.GreenSpaces, Average(reviews, review => review.GreenSpaceScore)),
                ]);
        }

        public static ReviewResponse ToResponse(DomainReview review, string? authorEmail, Guid? currentUserId) =>
            new(
                review.Id,
                AuthorInitials.GetInitials(authorEmail),
                review.YearsInZone,
                review.IsVerified,
                review.OverallScore,
                new ReviewAspectScoresResponse(
                    review.SafetyScore,
                    review.TransitScore,
                    review.ConnectivityScore,
                    review.CommerceScore,
                    review.GreenSpaceScore),
                review.Topics,
                review.Text,
                review.CreatedAt,
                review.HelpfulVotes.Count,
                currentUserId is not null && review.HelpfulVotes.Any(vote => vote.UserId == currentUserId),
                review.UserId == currentUserId);
    }
}

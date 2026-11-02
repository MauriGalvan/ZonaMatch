using ZonaMatch.Application.DTOs.Reviews.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Reviews
{
    // Lists a zone's reviews from newest to oldest and marks the current user's review when available.
    public class ListZoneReviews
    {
        private readonly IReviewRepository _reviews;

        public ListZoneReviews(IReviewRepository reviews)
        {
            _reviews = reviews;
        }

        public async Task<ZoneReviewListResponse> ExecuteAsync(string zoneSlug, Guid? currentUserId, CancellationToken cancellationToken = default)
        {
            var reviews = await _reviews.ListarPorZonaAsync(zoneSlug, cancellationToken);

            return new ZoneReviewListResponse(
                ReviewMapper.Summarize(reviews),
                reviews.Select(review => ReviewMapper.ToResponse(review, review.User?.Email, currentUserId)).ToList());
        }
    }
}

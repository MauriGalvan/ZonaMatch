using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Review
{
    // Lists a zone's approved reviews from newest to oldest, plus the current user's own review whatever its status,
    // and marks that review when available. An administrator also gets the pending reviews of other users, to moderate them.
    public class ListZoneReviews
    {
        private readonly IReviewRepository _reviews;
        private readonly IImageStorage _images;

        public ListZoneReviews(IReviewRepository reviews, IImageStorage images)
        {
            _reviews = reviews;
            _images = images;
        }

        public async Task<ZoneReviewListResponse> ExecuteAsync(string zoneSlug, Guid? currentUserId, bool isAdministrator = false, CancellationToken cancellationToken = default)
        {
            var reviews = await _reviews.ListByZoneAsync(zoneSlug, currentUserId, includeAllPending: isAdministrator, cancellationToken);

            // Only approved reviews count in the summary, so the author's pending review does not change what everybody sees.
            var approvedReviews = reviews.Where(review => review.Status == ReviewStatus.Approved).ToList();

            return new ZoneReviewListResponse(
                ReviewMapper.Summarize(approvedReviews),
                reviews.Select(review => ReviewMapper.ToResponse(review, review.User?.Email, currentUserId, _images)).ToList());
        }
    }
}

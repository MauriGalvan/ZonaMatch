using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Review
{
    // Lists the reviews of every zone that wait for a site administrator, oldest first, with the links to their images.
    // The API checks the administrator role.
    public class ListPendingReviews
    {
        private readonly IReviewRepository _reviews;
        private readonly IImageStorage _images;

        public ListPendingReviews(IReviewRepository reviews, IImageStorage images)
        {
            _reviews = reviews;
            _images = images;
        }

        public async Task<IReadOnlyList<PendingReviewResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var reviews = await _reviews.ListPendingAsync(cancellationToken);

            return reviews
                .Select(review => new PendingReviewResponse(
                    review.ZoneSlug,
                    ReviewMapper.ToResponse(review, review.User?.Email, currentUserId: null, _images)))
                .ToList();
        }
    }
}

using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Review
{
    public class DeleteMyReview
    {
        private readonly IReviewRepository _reviews;

        public DeleteMyReview(IReviewRepository reviews)
        {
            _reviews = reviews;
        }

        public async Task ExecuteAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default)
        {
            var review = await _reviews.GetByUserAsync(zoneSlug, userId, cancellationToken)
                ?? throw new NotFoundException("No escribiste una reseña de esta zona.");

            _reviews.Delete(review);
            await _reviews.SaveChangesAsync(cancellationToken);
        }
    }
}

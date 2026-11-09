using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Review
{
    public class DeleteMyReview
    {
        private readonly IReviewRepository _reviews;
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _clock;

        public DeleteMyReview(IReviewRepository reviews, INotificationRepository notifications, TimeProvider clock)
        {
            _reviews = reviews;
            _notifications = notifications;
            _clock = clock;
        }

        public async Task ExecuteAsync(string zoneSlug, Guid userId, CancellationToken cancellationToken = default)
        {
            var review = await _reviews.GetByUserAsync(zoneSlug, userId, cancellationToken)
                ?? throw new NotFoundException("No escribiste una reseña de esta zona.");

            _reviews.Delete(review);
            await _reviews.SaveChangesAsync(cancellationToken);

            // A review deleted while pending no longer needs moderation.
            await _notifications.MarkReviewModerationAsReadAsync(review.Id, _clock.GetUtcNow(), cancellationToken);
        }
    }
}

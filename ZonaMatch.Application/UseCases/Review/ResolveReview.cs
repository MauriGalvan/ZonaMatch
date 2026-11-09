using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Review
{
    // A site administrator approves or rejects a pending review. Approving it makes the review and its images visible
    // to everybody and, the first time, notifies the users who saved the zone. The author gets an email with the outcome.
    // The API checks the administrator role.
    public class ResolveReview
    {
        private readonly IReviewRepository _reviews;
        private readonly ISavedZoneRepository _savedZones;
        private readonly INotificationRepository _notifications;
        private readonly ReviewModerationEmails _moderationEmails;
        private readonly TimeProvider _clock;

        public ResolveReview(
            IReviewRepository reviews,
            ISavedZoneRepository savedZones,
            INotificationRepository notifications,
            ReviewModerationEmails moderationEmails,
            TimeProvider clock)
        {
            _reviews = reviews;
            _savedZones = savedZones;
            _notifications = notifications;
            _moderationEmails = moderationEmails;
            _clock = clock;
        }

        public async Task ExecuteAsync(
            string zoneSlug, Guid reviewId, Guid administratorId, bool approve, string? reason, CancellationToken cancellationToken = default)
        {
            var review = await _reviews.GetAsync(zoneSlug, reviewId, cancellationToken)
                ?? throw new NotFoundException("La reseña no existe.");

            if (review.Status != ReviewStatus.Pending)
                throw new ConflictException("La reseña ya fue resuelta.");

            var rejectionReason = reason?.Trim();
            if (!approve && string.IsNullOrEmpty(rejectionReason))
                throw new BadRequestException("Indicá el motivo del rechazo.");

            var now = _clock.GetUtcNow();

            // An edited review that is approved again was already announced, so only its first approval notifies.
            var isFirstPublication = approve && review.PublishedAt is null;
            review.Resolve(administratorId, approve, rejectionReason, now);

            if (isFirstPublication)
            {
                var savedZones = await _savedZones.GetForZoneAsync(zoneSlug, review.UserId, cancellationToken);
                _notifications.AddRange(savedZones.Select(savedZone => new UserNotification
                {
                    UserId = savedZone.UserId,
                    Type = NotificationType.SavedZoneReview,
                    Title = "Nueva reseña en una zona guardada",
                    Message = $"Publicaron una nueva reseña en {savedZone.Name}.",
                    Link = $"/zonas/{Uri.EscapeDataString(zoneSlug)}?tab=comunidad",
                    CreatedAt = now
                }));
            }

            await _reviews.SaveChangesAsync(cancellationToken);

            // The review no longer waits for anyone: clear it from the bell of every administrator.
            await _notifications.MarkReviewModerationAsReadAsync(review.Id, now, cancellationToken);

            await _moderationEmails.NotifyResolvedAsync(review, review.User?.Email, approve, cancellationToken);
        }
    }
}

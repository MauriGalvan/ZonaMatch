using ZonaMatch.Application.DTOs.Review.Requests;
using ZonaMatch.Application.DTOs.Review.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Application.UseCases.Image;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using DomainReview = ZonaMatch.Domain.Entities.Review;

namespace ZonaMatch.Application.UseCases.Review
{
    // Creates or replaces the user's review for a zone (one per user and zone). A new review, or one whose text or images
    // changed, waits for a site administrator's approval and is visible only to its author until then.
    public class SaveMyReview
    {
        private readonly IReviewRepository _reviews;
        private readonly IZoneRepository _zones;
        private readonly IImageStorage _images;
        private readonly IUserRepository _users;
        private readonly INotificationRepository _notifications;
        private readonly ReviewModerationEmails _moderationEmails;
        private readonly TimeProvider _clock;

        public SaveMyReview(
            IReviewRepository reviews,
            IZoneRepository zones,
            IImageStorage images,
            IUserRepository users,
            INotificationRepository notifications,
            ReviewModerationEmails moderationEmails,
            TimeProvider clock)
        {
            _reviews = reviews;
            _zones = zones;
            _images = images;
            _users = users;
            _notifications = notifications;
            _moderationEmails = moderationEmails;
            _clock = clock;
        }

        public async Task<ReviewResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, SaveReviewRequest request, CancellationToken cancellationToken = default)
        {
            await _zones.EnsureZoneExistsAsync(zoneSlug, cancellationToken);

            if (!ReviewAspect.TryNormalize(request.Topics, out var topics, out var error))
                throw new BadRequestException(error);

            var imageNames = NormalizeImageNames(request.ImageNames);
            var text = request.Text.Trim();

            var now = _clock.GetUtcNow();
            var review = await _reviews.GetByUserAsync(zoneSlug, currentUser.Id, cancellationToken);
            var isNewReview = review is null;
            var submittedForApproval = isNewReview;

            if (review is null)
            {
                // Pending by default: it stays hidden from the community until an administrator approves it.
                review = new DomainReview { ZoneSlug = zoneSlug, UserId = currentUser.Id, CreatedAt = now };
                _reviews.Add(review);
            }
            else if (review.Status == ReviewStatus.Rejected
                || (review.Status == ReviewStatus.Approved && (review.Text != text || !review.ImageNames.SequenceEqual(imageNames))))
            {
                // What the community sees changed, or a rejected review is being corrected: it needs a new approval.
                review.ReturnToPending();
                submittedForApproval = true;
            }

            if (submittedForApproval)
                await NotifyAdministratorsAsync(review.Id, zoneSlug, isEdit: !isNewReview, now, cancellationToken);

            var scores = request.Aspects!; // Required by request validation.
            review.SafetyScore = scores.Safety;
            review.TransitScore = scores.Transport;
            review.ConnectivityScore = scores.Connectivity;
            review.CommerceScore = scores.Commerce;
            review.GreenSpaceScore = scores.GreenSpaces;
            review.Text = text;
            review.YearsInZone = request.YearsInZone;
            review.Topics = topics.ToList();
            review.ImageNames = imageNames;
            review.UpdatedAt = now;

            await _reviews.SaveChangesAsync(cancellationToken);

            // Only a review that enters the approval queue is announced: editing one that is still pending does not
            // email the author and the administrators again.
            if (submittedForApproval)
                await _moderationEmails.NotifySubmittedAsync(review, currentUser.Email, isEdit: !isNewReview, cancellationToken);

            return ReviewMapper.ToResponse(review, currentUser.Email, currentUser.Id, _images);
        }

        // The bell of every administrator shows a review to moderate. It is saved together with the review.
        private async Task NotifyAdministratorsAsync(
            Guid reviewId, string zoneSlug, bool isEdit, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var administratorIds = await _users.GetAdministratorIdsAsync(cancellationToken);

            _notifications.AddRange(administratorIds.Select(id => new UserNotification
            {
                UserId = id,
                Type = NotificationType.ReviewPendingModeration,
                Title = isEdit ? "Una reseña editada espera tu revisión" : "Hay una reseña para revisar",
                Message = $"Hay una reseña pendiente de aprobación en {zoneSlug}.",
                Link = $"/zonas/{Uri.EscapeDataString(zoneSlug)}?tab=comunidad",
                ReviewId = reviewId,
                CreatedAt = now
            }));
        }

        // The image names come from POST /api/v1/images; anything else is rejected.
        private static List<string> NormalizeImageNames(IReadOnlyList<string>? names)
        {
            var distinct = (names ?? []).Distinct().ToList();

            if (distinct.Count > DomainReview.MaximumImages)
                throw new BadRequestException($"Una reseña admite hasta {DomainReview.MaximumImages} imágenes.");

            if (distinct.Any(name => !UploadImage.IsValidName(name)))
                throw new BadRequestException("Alguna de las imágenes no es válida.");

            return distinct;
        }
    }
}

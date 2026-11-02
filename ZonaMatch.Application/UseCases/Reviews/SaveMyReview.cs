using ZonaMatch.Application.DTOs.Reviews.Requests;
using ZonaMatch.Application.DTOs.Reviews.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Community;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;

namespace ZonaMatch.Application.UseCases.Reviews
{
    // Creates or replaces the user's review for a zone (one per user and zone).
    public class SaveMyReview
    {
        private readonly IReviewRepository _reviews;
        private readonly IZoneRepository _zones;
        private readonly ISavedZoneRepository _savedZones;
        private readonly INotificationRepository _notifications;
        private readonly TimeProvider _clock;

        public SaveMyReview(
            IReviewRepository reviews,
            IZoneRepository zones,
            ISavedZoneRepository savedZones,
            INotificationRepository notifications,
            TimeProvider clock)
        {
            _reviews = reviews;
            _zones = zones;
            _savedZones = savedZones;
            _notifications = notifications;
            _clock = clock;
        }

        public async Task<ReviewResponse> ExecuteAsync(
            string zoneSlug, AuthenticatedUserContext currentUser, SaveReviewRequest request, CancellationToken cancellationToken = default)
        {
            await _zones.EnsureZoneExistsAsync(zoneSlug, cancellationToken);

            if (!ReviewAspect.TryNormalize(request.Topics, out var topics, out var error))
                throw new BadRequestException(error);

            var now = _clock.GetUtcNow();
            var review = await _reviews.GetByUserAsync(zoneSlug, currentUser.Id, cancellationToken);
            var isNewReview = review is null;

            if (review is null)
            {
                review = new Review { ZoneSlug = zoneSlug, UserId = currentUser.Id, CreatedAt = now };
                _reviews.Add(review);
            }

            var scores = request.Aspects!; // Required by request validation.
            review.SafetyScore = scores.Safety;
            review.TransitScore = scores.Transport;
            review.ConnectivityScore = scores.Connectivity;
            review.CommerceScore = scores.Commerce;
            review.GreenSpaceScore = scores.GreenSpaces;
            review.Text = request.Text.Trim();
            review.YearsInZone = request.YearsInZone;
            review.Topics = topics.ToList();
            review.UpdatedAt = now;

            if (isNewReview)
            {
                var savedZones = await _savedZones.GetForZoneAsync(zoneSlug, currentUser.Id, cancellationToken);
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

            return ReviewMapper.ToResponse(review, currentUser.Email, currentUser.Id);
        }
    }
}

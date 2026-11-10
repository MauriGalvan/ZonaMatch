using System.Text.Json.Serialization;

namespace ZonaMatch.Domain.Entities
{
    public enum ReviewStatus
    {
        // Waiting for a site administrator. Only its author sees it.
        [JsonStringEnumMemberName("pendiente")]
        Pending,

        [JsonStringEnumMemberName("aprobada")]
        Approved,

        [JsonStringEnumMemberName("rechazada")]
        Rejected
    }

    // A community member's review of a zone (app.resenas table). One per user and zone; the author can edit it.
    public class Review
    {
        public const int MinimumScore = 1;
        public const int MaximumScore = 5;
        public const int MaximumImages = 3;

        public Guid Id { get; set; } = Guid.CreateVersion7();

        // Zone slug, as in GET /api/v1/zones/{slug}.
        public string ZoneSlug { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public User? User { get; set; }

        // Score for each aspect (ReviewAspect), from 1 to 5.
        public int SafetyScore { get; set; }
        public int TransitScore { get; set; }
        public int ConnectivityScore { get; set; }
        public int CommerceScore { get; set; }
        public int GreenSpaceScore { get; set; }

        // Overall score: the unrounded average of the aspect scores. Not stored in the database.
        public double OverallScore =>
            (SafetyScore + TransitScore + ConnectivityScore + CommerceScore + GreenSpaceScore) / 5.0;

        public string Text { get; set; } = string.Empty;

        // How long the author has lived in the zone, as reported by the author (null if not provided).
        public int? YearsInZone { get; set; }

        // ReviewAspect codes mentioned in the review, used by review filters.
        public List<string> Topics { get; set; } = [];

        // Names of the review's images in blob storage (see IImageStorage), at most MaximumImages.
        public List<string> ImageNames { get; set; } = [];

        // Whether the platform verified the residence. No verification process exists yet, so this is always false.
        public bool IsVerified { get; set; }

        // Moderation: a new review is pending until a site administrator approves it, and until then only its author
        // sees it. Its images (ImageNames) follow the review's status.
        public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

        // Why an administrator rejected it. Only the author sees it.
        public string? RejectionReason { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }

        // The administrator who approved or rejected it.
        public Guid? ResolvedByUserId { get; set; }

        // When it was first approved. An edit sends the review back to pending but keeps this date, so the zone's
        // subscribers are notified only once.
        public DateTimeOffset? PublishedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public List<ReviewHelpfulVote> HelpfulVotes { get; set; } = [];

        // A site administrator approves or rejects a pending review. The caller checks that it is pending.
        public void Resolve(Guid administratorId, bool approve, string? rejectionReason, DateTimeOffset now)
        {
            Status = approve ? ReviewStatus.Approved : ReviewStatus.Rejected;
            RejectionReason = approve ? null : rejectionReason;
            ResolvedAt = now;
            ResolvedByUserId = administratorId;

            if (approve)
                PublishedAt ??= now;
        }

        // The author changed what the community sees, or is correcting a rejected review: it needs a new approval.
        public void ReturnToPending()
        {
            Status = ReviewStatus.Pending;
            RejectionReason = null;
            ResolvedAt = null;
            ResolvedByUserId = null;
        }
    }

    // A user's "helpful" vote on a review (app.resenas_votos_util table).
    public class ReviewHelpfulVote
    {
        public Guid ReviewId { get; set; }
        public Guid UserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

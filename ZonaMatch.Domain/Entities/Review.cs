namespace ZonaMatch.Domain.Entities
{
    // A community member's review of a zone (app.resenas table). One per user and zone; the author can edit it.
    public class Review
    {
        public const int MinimumScore = 1;
        public const int MaximumScore = 5;

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

        // Whether the platform verified the residence. No verification process exists yet, so this is always false.
        public bool IsVerified { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public List<ReviewHelpfulVote> HelpfulVotes { get; set; } = [];
    }

    // A user's "helpful" vote on a review (app.resenas_votos_util table).
    public class ReviewHelpfulVote
    {
        public Guid ReviewId { get; set; }
        public Guid UserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

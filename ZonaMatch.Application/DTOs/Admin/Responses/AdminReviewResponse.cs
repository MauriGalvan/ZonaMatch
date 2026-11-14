namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/reviews: reviews waiting for approval (oldest first) and the latest approved or rejected ones (most
// recently resolved first).
public sealed record AdminReviewListResponse(IReadOnlyList<AdminReviewResponse> Pending, IReadOnlyList<AdminReviewResponse> History);

// A review for the admin panel. Status: pending, approved or rejected. ZoneName falls back to the slug when the zone has
// no data. RejectionReason, ResolvedAt and ResolvedByEmail are null while the review is pending.
public sealed record AdminReviewResponse(
    Guid Id,
    string ZoneSlug,
    string ZoneName,
    string AuthorEmail,
    double OverallScore,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Status,
    string? RejectionReason,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByEmail);

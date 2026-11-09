namespace ZonaMatch.Application.DTOs.Review.Responses;

// A review waiting for approval, with the zone it belongs to.
public sealed record PendingReviewResponse(string ZoneSlug, ReviewResponse Review);

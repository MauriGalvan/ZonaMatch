namespace ZonaMatch.Application.DTOs.Review.Responses;

public sealed record ZoneReviewListResponse(ReviewSummaryResponse Summary, IReadOnlyList<ReviewResponse> Reviews);

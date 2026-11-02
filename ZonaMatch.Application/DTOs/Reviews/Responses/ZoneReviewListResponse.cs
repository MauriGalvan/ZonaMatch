namespace ZonaMatch.Application.DTOs.Reviews.Responses;

public sealed record ZoneReviewListResponse(ReviewSummaryResponse Summary, IReadOnlyList<ReviewResponse> Reviews);

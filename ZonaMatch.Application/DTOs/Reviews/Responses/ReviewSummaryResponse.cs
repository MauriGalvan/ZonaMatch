namespace ZonaMatch.Application.DTOs.Reviews.Responses;

public sealed record ReviewSummaryResponse(
    double Average,
    int Total,
    int Verified,
    IReadOnlyList<ReviewAspectRatingResponse> Aspects);

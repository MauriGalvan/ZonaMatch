namespace ZonaMatch.Application.DTOs.Review.Responses;

public sealed record ReviewSummaryResponse(
    double Average,
    int Total,
    int Verified,
    IReadOnlyList<ReviewAspectRatingResponse> Aspects);

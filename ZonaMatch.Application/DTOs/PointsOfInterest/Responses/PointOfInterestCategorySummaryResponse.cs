namespace ZonaMatch.Application.DTOs.PointsOfInterest.Responses;

// Every category is included, with Total set to zero when it has no points.
public sealed record PointOfInterestCategorySummaryResponse(
    string Category,
    int Total,
    IReadOnlyList<PointOfInterestTypeCountResponse> Types);

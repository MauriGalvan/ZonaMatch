namespace ZonaMatch.Application.DTOs.PointOfInterest.Responses;

// Counts points by category and OSM type inside a radius or zone. RadiusMeters is null for zone queries.
public sealed record PointsOfInterestSummaryResponse(
    double? RadiusMeters,
    IReadOnlyList<PointOfInterestCategorySummaryResponse> Categories);

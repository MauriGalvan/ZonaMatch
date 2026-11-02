namespace ZonaMatch.Application.DTOs.PointsOfInterest.Responses;

// Counts points by category and OSM type inside a radius or zone. RadioMetros is null for zone queries.
public sealed record PointsOfInterestSummaryResponse(
    double? RadioMetros,
    IReadOnlyList<PointOfInterestCategorySummaryResponse> Categorias);

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record AnalysisPointsResponse(
    string? RentLimit,
    IReadOnlyList<AnalysisPlaceResponse> Places,
    string Operation = "alquiler",
    decimal? PurchaseLimitUsd = null,
    string? PropertyType = null,
    int? AreaM2 = null,
    int? Rooms = null,
    bool CreditEligible = false);

namespace ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

public sealed record GroupRankingResponse(
    IReadOnlyList<RankedZoneResponse> Zones,
    IReadOnlyList<ExcludedParticipantResponse> ExcludedParticipants,
    IReadOnlyList<GroupPointOfInterestResponse> ParticipantPoints,
    decimal? MaximumRent = null,
    IReadOnlyList<RankedZoneResponse>? NearbyZones = null,
    string Operation = "alquiler",
    decimal? MaximumPurchaseUsd = null,
    string? PropertyType = null,
    GroupProfileRecommendationsResponse? ProfileRecommendations = null,
    int? SurfaceM2 = null);

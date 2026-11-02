using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record ComparedAnalysisResponse(
    Guid Id,
    string Name,
    string TravelMode,
    IReadOnlyList<ComparedZoneResponse> Zones,
    GroupRankingResponse? GroupScores = null);

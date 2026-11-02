using ZonaMatch.Application.DTOs.GroupAnalysis.Responses;
using ZonaMatch.Application.DTOs.Scoring.Responses;

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record AnalysisResponse(
    Guid Id,
    string Name,
    Guid? OriginAnalysisId,
    IReadOnlyList<AnalysisZoneResponse> Zones,
    AnalysisContextResponse Context,
    Dictionary<string, string> Criteria,
    AnalysisPointsResponse Points,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    GroupRankingResponse? GroupScores,
    IReadOnlyList<AnalysisResponse> Versions,
    // Result saved with a personal analysis and when it was calculated; null when it is scored live
    ScoresResponse? Result = null,
    DateTimeOffset? CalculatedAt = null,
    // Starred in the user's "Mis análisis" list.
    bool IsFavorite = false);

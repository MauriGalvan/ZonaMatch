using ZonaMatch.Application.DTOs.Analysis.Responses;

namespace ZonaMatch.Application.DTOs.Profile.Responses;

public sealed record ProfileResponse(
    AnalysisContextResponse Context,
    Dictionary<string, string> Criteria,
    AnalysisPointsResponse Points,
    DateTimeOffset UpdatedAt);

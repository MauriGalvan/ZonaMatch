namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record TrashedAnalysisResponse(
    Guid Id,
    string Name,
    Guid? OriginalAnalysisId,
    bool IsGroupAnalysis,
    DateTimeOffset DeletedAt,
    DateTimeOffset ExpiresAt);

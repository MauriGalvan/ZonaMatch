namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record AnalysisComparisonResponse(IReadOnlyList<ComparedAnalysisResponse> Analyses);

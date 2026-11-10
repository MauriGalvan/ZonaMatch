namespace ZonaMatch.Application.DTOs.AnalysisFollowUp.Responses;

public sealed record AnalysisFollowUpResponse(Guid Id, Guid AnalysisId, string AnalysisName, DateTimeOffset AnalysisCreatedAt);

namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record ComparedScoreComponentResponse(string Criterion, string Priority, int? Score, string Details);

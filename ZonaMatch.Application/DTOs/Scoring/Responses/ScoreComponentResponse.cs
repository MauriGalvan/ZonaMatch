namespace ZonaMatch.Application.DTOs.Scoring.Responses;

public sealed record ScoreComponentResponse(string Criterion, string Priority, int? Score, string Details);

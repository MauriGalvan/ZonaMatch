namespace ZonaMatch.Application.DTOs.Analysis.Responses;

public sealed record ComparedScoreLimitResponse(string Type, bool? IsMet, string Details);

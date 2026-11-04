namespace ZonaMatch.Application.DTOs.Scoring.Responses;

/// <summary>IsMet is null when there is not enough data to determine the result.</summary>
public sealed record ScoreLimitResponse(string Type, bool? IsMet, string Details);

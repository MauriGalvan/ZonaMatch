namespace ZonaMatch.Application.DTOs.Investor.Responses;

// The user's investor profile; Configured is false until it is saved for the first time.
public sealed record InvestorProfileResponse(
    bool Configured,
    IReadOnlyDictionary<string, string> Criteria,
    decimal? CapitalUsd,
    bool CreditEligible,
    string? PropertyType,
    int? Rooms,
    DateTimeOffset? UpdatedAt);

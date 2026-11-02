namespace ZonaMatch.Application.DTOs.Review.Responses;

public sealed record ReviewResponse(
    Guid Id,
    string Initials,
    int? YearsInZone,
    bool Verified,
    double Rating,
    ReviewAspectScoresResponse Aspects,
    IReadOnlyList<string> Topics,
    string Text,
    DateTimeOffset CreatedAt,
    int HelpfulCount,
    bool MarkedHelpfulByMe,
    bool IsMine);

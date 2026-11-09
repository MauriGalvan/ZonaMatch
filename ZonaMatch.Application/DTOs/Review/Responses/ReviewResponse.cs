using ZonaMatch.Application.DTOs.Image.Responses;
using ZonaMatch.Domain.Entities;

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
    bool IsMine,
    IReadOnlyList<ImageResponse> Images,
    ReviewStatus Status,
    // Only filled in for the author of a rejected review.
    string? RejectionReason);

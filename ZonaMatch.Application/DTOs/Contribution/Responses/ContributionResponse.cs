using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.DTOs.Contribution.Responses;

public sealed record ContributionResponse(
    Guid Id,
    ContributionType Type,
    ContributionStatus Status,
    string AuthorInitials,
    DateTimeOffset CreatedAt,
    bool IsMine,
    bool? MyVote,
    int Confirmations,
    int Rejections,
    string? Name,
    string? Category,
    ContributionLocationResponse? Location,
    string? Schedule,
    string? Address,
    bool LivesOrWorksInZone,
    string? PointOfInterestId,
    string? PointOfInterestName,
    CorrectionReason? Reason,
    string? Comment);

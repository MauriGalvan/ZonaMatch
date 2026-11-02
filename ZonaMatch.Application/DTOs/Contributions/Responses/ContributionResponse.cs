using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.DTOs.Contributions.Responses;

public sealed record ContributionResponse(
    Guid Id,
    TipoAporte Type,
    EstadoAporte Status,
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
    MotivoCorreccion? Reason,
    string? Comment);

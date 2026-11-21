namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/contributions: contributions waiting for a decision (oldest first) and the latest approved or
// rejected ones (most recently resolved first).
public sealed record AdminContributionListResponse(
    IReadOnlyList<AdminContributionResponse> Pending,
    IReadOnlyList<AdminContributionResponse> History);

// A community contribution to a zone map. Type: new-point or correction. Status: pending, approved or rejected.
// Name and Category: the new point, or the proposed value of a correction. PointOfInterestName and Reason: the corrected
// place and what is wrong with it (closed, name, location, category, other). ResolvedByEmail: the administrator who
// resolved it; null with ResolvedAt set means the community votes resolved it.
public sealed record AdminContributionResponse(
    Guid Id,
    string ZoneSlug,
    string ZoneName,
    string AuthorEmail,
    string Type,
    string? Name,
    string? Category,
    string? Address,
    string? PointOfInterestName,
    string? Reason,
    string? Comment,
    int Confirmations,
    int Rejections,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByEmail);

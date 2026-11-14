namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/stats/groups: a group search with its members. CreatedAt is in UTC.
public sealed record AdminGroupResponse(Guid Id, string Name, DateTimeOffset CreatedAt, IReadOnlyList<AdminGroupMemberResponse> Members);

// A member of a group; IsOwner marks the user who created it.
public sealed record AdminGroupMemberResponse(Guid UserId, string Email, bool IsOwner);

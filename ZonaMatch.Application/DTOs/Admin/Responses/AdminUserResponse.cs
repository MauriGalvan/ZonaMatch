namespace ZonaMatch.Application.DTOs.Admin.Responses;

// A registered user as shown in the admin user list. InvestorAccess: none, requested or granted. RegisteredAt: when
// the account was created.
public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    DateOnly DateOfBirth,
    bool IsAdmin,
    string InvestorAccess = "none",
    DateTimeOffset? InvestorAccessRequestedAt = null,
    DateTimeOffset? RegisteredAt = null);

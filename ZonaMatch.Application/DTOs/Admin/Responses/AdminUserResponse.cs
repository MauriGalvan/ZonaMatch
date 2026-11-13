namespace ZonaMatch.Application.DTOs.Admin.Responses;

// A registered user as shown in the admin user list. InvestorAccess: none, requested or granted.
public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    DateOnly DateOfBirth,
    bool IsAdmin,
    string InvestorAccess = "none",
    DateTimeOffset? InvestorAccessRequestedAt = null);

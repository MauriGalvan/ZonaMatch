namespace ZonaMatch.Application.DTOs.Admin.Responses;

// A registered user as shown in the admin user list.
public sealed record AdminUserResponse(Guid Id, string Email, DateOnly DateOfBirth, bool IsAdmin);

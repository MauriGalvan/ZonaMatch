namespace ZonaMatch.Application.DTOs.Auth.Responses;

public sealed record CurrentSessionResponse(
    Guid Id,
    string Email,
    bool IsAdmin = false,
    string? FirstName = null,
    string? LastName = null);

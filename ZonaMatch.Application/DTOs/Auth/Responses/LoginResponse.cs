namespace ZonaMatch.Application.DTOs.Auth.Responses;

public sealed record LoginResponse(DateTimeOffset ExpiresAt, UserResponse User);

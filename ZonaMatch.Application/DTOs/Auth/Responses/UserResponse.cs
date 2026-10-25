namespace ZonaMatch.Application.DTOs.Auth.Responses;

public sealed record UserResponse(Guid Id, string Email, DateOnly BirthDate, bool IsAdmin = false);

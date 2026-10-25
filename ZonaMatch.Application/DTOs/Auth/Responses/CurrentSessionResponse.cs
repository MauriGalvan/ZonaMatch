namespace ZonaMatch.Application.DTOs.Auth.Responses;

public sealed record CurrentSessionResponse(Guid Id, string Email, bool IsAdmin = false);

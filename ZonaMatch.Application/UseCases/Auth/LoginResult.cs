using ZonaMatch.Application.DTOs.Auth.Responses;

namespace ZonaMatch.Application.UseCases.Auth;

public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

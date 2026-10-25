using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Observability;

namespace ZonaMatch.Application.UseCases.Auth;

public sealed class LoginUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<LoginUser> logger)
{
    public async Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            // Same message for unknown email and wrong password (no user enumeration), but the log keeps a correlatable hash
            logger.LogWarning("Failed login attempt for {EmailHash}", LogHash.Of(normalizedEmail));
            throw new UnauthorizedException("Email o contrasena incorrectos.");
        }

        var (accessToken, expiresAt) = tokenService.Generate(user);
        var userResponse = new UserResponse(user.Id, user.Email, user.FechaNacimiento);

        logger.LogInformation("User {UserId} logged in", user.Id);

        return new LoginResult(accessToken, expiresAt, userResponse);
    }
}

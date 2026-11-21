using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Observability;

namespace ZonaMatch.Application.UseCases.Auth;

// Checks the suspension after the password, so a wrong password never reveals that an account exists or is suspended.
public sealed class LoginUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<LoginUser> logger)
{
    public const string SuspendedAccountMessage =
        "Su cuenta ha sido suspendida. Para más información sobre el estado de su cuenta, contáctenos por email.";

    public async Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            // Same message for unknown email and wrong password (no user enumeration), but the log keeps a correlatable hash
            logger.LogWarning("Failed login attempt for {EmailHash}", LogHash.Of(normalizedEmail));
            throw new UnauthorizedException("Email o contraseña incorrectos.");
        }

        if (user.IsSuspended)
        {
            logger.LogWarning("Suspended user {UserId} tried to log in", user.Id);
            throw new ForbiddenException(SuspendedAccountMessage);
        }

        var (accessToken, expiresAt) = tokenService.Generate(user);
        var userResponse = new UserResponse(user.Id, user.Email, user.DateOfBirth, user.IsAdministrator, user.FirstName, user.LastName);

        logger.LogInformation("User {UserId} logged in", user.Id);

        return new LoginResult(accessToken, expiresAt, userResponse);
    }
}

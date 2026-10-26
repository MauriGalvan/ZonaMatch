using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.PasswordResets.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.PasswordResets;

// Sets the new password with the token of the emailed link. The link works once; the other pending links of the
// user stop working too.
public sealed class CompletePasswordReset(
    IPasswordResetRepository resets,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    ILogger<CompletePasswordReset> logger)
{
    public const string InvalidLinkMessage = "El enlace no es válido o ya venció. Pedí uno nuevo para cambiar tu contraseña.";

    public async Task ExecuteAsync(CompletePasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var token = await resets.GetByTokenHashAsync(PasswordResetTokens.Hash(request.Token.Trim()), cancellationToken);
        if (token is null || !token.IsUsable(now))
            throw new BadRequestException(InvalidLinkMessage);

        await resets.ResetPasswordAsync(token, passwordHasher.Hash(request.Password), now, cancellationToken);
        logger.LogInformation("User {UserId} chose a new password", token.UserId);
    }
}

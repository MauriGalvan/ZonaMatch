using Microsoft.Extensions.Logging;
using ZonaMatch.Application.DTOs.PasswordResets.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Observability;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.PasswordResets;

// Emails a one-time link to choose a new password. The answer is always the same, whether the email exists or not,
// so the endpoint cannot be used to find out who has an account.
public sealed class RequestPasswordReset(
    IUserRepository users,
    IPasswordResetRepository resets,
    IEmailService email,
    FrontendLinks links,
    TimeProvider timeProvider,
    ILogger<RequestPasswordReset> logger)
{
    public const int MaxLinksPerHour = 3;
    public const string Subject = "Cambiá tu contraseña de ZonaMatch";

    public async Task ExecuteAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            logger.LogInformation("Password reset requested for an unknown email {EmailHash}", LogHash.Of(normalizedEmail));
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (await resets.CountCreatedSinceAsync(user.Id, now.AddHours(-1), cancellationToken) >= MaxLinksPerHour)
        {
            logger.LogWarning("Password reset limit reached for user {UserId}", user.Id);
            return;
        }

        var token = PasswordResetTokens.New();
        await resets.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = PasswordResetTokens.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(PasswordResetTokens.Lifetime),
        }, cancellationToken);

        try
        {
            await email.SendEmailAsync(user.Email, Subject, Body(links.To($"/restablecer-contrasena?token={token}")), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An error here would tell apart registered emails: it is logged and the answer stays the same
            logger.LogError(ex, "Could not email the password reset link to user {UserId}", user.Id);
        }
    }

    private static string Body(string link) => BrandEmail.Render(new BrandEmailContent(
        Preheader: "Elegí una contraseña nueva para tu cuenta.",
        Title: "Elegí una contraseña nueva",
        Paragraphs:
        [
            "Recibimos un pedido para cambiar la contraseña de tu cuenta de ZonaMatch.",
            "Tocá el botón para elegir una nueva. El enlace sirve una sola vez y vence en 1 hora.",
        ],
        Button: new BrandEmailButton("Elegir contraseña nueva", link),
        Footnote: "Si no lo pediste vos, ignorá este mail: tu contraseña sigue siendo la misma."));
}

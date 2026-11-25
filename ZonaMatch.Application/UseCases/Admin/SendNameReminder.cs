using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Admin;

// An administrator emails a user who has not completed their first and last name yet. Signing in is enough: the site
// asks for the missing name right after the login.
public sealed class SendNameReminder(IUserRepository users, IEmailService email, FrontendLinks links)
{
    public const string Subject = "Completá tus datos en ZonaMatch";

    public async Task ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("El usuario no existe.");

        if (!string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(user.LastName))
            throw new BadRequestException("Este usuario ya completó su nombre y apellido.");

        try
        {
            await email.SendEmailAsync(user.Email, Subject, Body(links), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ServiceUnavailableException("No pudimos enviar el email en este momento. Intentá de nuevo en unos minutos.", ex);
        }
    }

    private static string Body(FrontendLinks links) => BrandEmail.Render(new BrandEmailContent(
        Preheader: "Te falta completar tu nombre y apellido.",
        Title: "Completá tus datos",
        Paragraphs:
        [
            "Para seguir usando ZonaMatch nos falta tu nombre y apellido.",
            "Iniciá sesión y te los vamos a pedir apenas entres: es un paso de menos de un minuto.",
        ],
        Button: new BrandEmailButton("Iniciar sesión", links.To("/login")),
        Footnote: "Recibiste este mail porque tenés una cuenta en ZonaMatch."));
}

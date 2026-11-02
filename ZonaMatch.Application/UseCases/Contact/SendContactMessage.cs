using ZonaMatch.Application.DTOs.Contact.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Contact;

public sealed class SendContactMessage(IEmailService emailService, ContactOptions contactOptions, TimeProvider timeProvider)
{
    // Argentina does not observe daylight saving time; its offset remains UTC-3.
    private static readonly TimeSpan ArgentinaTimeOffset = TimeSpan.FromHours(-3);

    public async Task ExecuteAsync(SendContactMessageRequest request, CancellationToken cancellationToken = default)
    {
        // Ignore bot submissions while returning the same successful response as a real submission.
        if (!string.IsNullOrWhiteSpace(request.Website))
            return;

        var email = request.Email.Trim();
        var name = Normalize(request.Name);
        var zone = Normalize(request.Zone);
        var date = timeProvider.GetUtcNow().ToOffset(ArgentinaTimeOffset);

        var subject = $"Formulario de contacto: {name ?? email}";
        // Encode submitted values so they cannot inject HTML into the email.
        var body = BrandEmail.Render(new BrandEmailContent(
            Preheader: $"Nuevo mensaje de {name ?? email}",
            Title: "Nuevo mensaje del formulario de contacto",
            Paragraphs:
            [
                $"<strong>Email:</strong> {BrandEmail.Encode(email)}",
                $"<strong>Nombre:</strong> {BrandEmail.Encode(name ?? "No lo indico")}",
                $"<strong>Dónde busca:</strong> {BrandEmail.Encode(zone ?? "No lo indico")}",
                $"<strong>Fecha:</strong> {date:dd/MM/yyyy HH:mm} (hora de Argentina)",
            ],
            Button: new BrandEmailButton("Responder", $"mailto:{email}")));

        try
        {
            await emailService.SendEmailAsync(contactOptions.RecipientEmail, subject, body, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ServiceUnavailableException("No pudimos enviar el formulario en este momento. Intenta de nuevo en unos minutos.", exception);
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

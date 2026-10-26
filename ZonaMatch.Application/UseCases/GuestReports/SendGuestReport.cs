using System.Net;
using System.Text;
using ZonaMatch.Application.DTOs.GuestReports.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.GuestReports;

// Emails a visitor the PDF report of their analysis, with an invitation to create an account.
// The PDF is built in the browser, so the email text is fixed and only real, small PDFs are attached:
// the endpoint cannot be used to send arbitrary content.
public sealed class SendGuestReport(IEmailService email, GuestReportQuota quota)
{
    public const int MaxReportBytes = 5 * 1024 * 1024;
    public const int MaxEmailsPerRecipientPerDay = 3;
    public const string Subject = "Tu informe de ZonaMatch";
    public const string AttachmentName = "informe-zonamatch.pdf";

    private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

    public async Task ExecuteAsync(SendGuestReportRequest request, byte[]? report, string frontendBaseUrl, CancellationToken cancellationToken = default)
    {
        // A bot filled in the hidden field: answer as if it worked, but send nothing
        if (!string.IsNullOrWhiteSpace(request.Website))
            return;

        if (report is null || report.Length == 0 || report.Length > MaxReportBytes || !report.AsSpan().StartsWith(PdfSignature))
            throw new BadRequestException("El informe no es un PDF válido.");

        var recipient = request.Email.Trim();
        var sent = quota.SentToday(recipient);
        if (sent >= MaxEmailsPerRecipientPerDay)
            throw new BadRequestException("Ya te mandamos varios informes hoy. Probá de nuevo mañana o creá tu cuenta para guardar tus análisis.");

        try
        {
            await email.SendEmailAsync(recipient, Subject, Body(frontendBaseUrl.TrimEnd('/')),
                [new EmailAttachment(AttachmentName, "application/pdf", report)], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ServiceUnavailableException("No pudimos enviar el informe en este momento. Intentá de nuevo en unos minutos.", ex);
        }

        quota.Register(recipient);
    }

    private static string Body(string frontendBaseUrl)
    {
        var registerUrl = WebUtility.HtmlEncode($"{frontendBaseUrl}/register");

        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;color:#1f2937;line-height:1.5">
              <h2 style="color:#0b3d91">¡Gracias por probar ZonaMatch!</h2>
              <p>Te mandamos adjunto el informe de tu análisis, con las zonas que mejor se ajustan a lo que te importa.</p>
              <p>Si te creás una cuenta podés guardar tus análisis, ajustarlos cuando cambien tus planes, compararlos y buscar zona en grupo con quienes vas a vivir.</p>
              <p style="margin:24px 0">
                <a href="{registerUrl}" style="background:#0b3d91;color:#ffffff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:bold">Crear mi cuenta gratis</a>
              </p>
              <p>¡Suerte con la búsqueda!<br>El equipo de ZonaMatch</p>
              <p style="font-size:12px;color:#6b7280">Recibiste este mail porque lo pediste desde ZonaMatch. No guardamos tu dirección ni te vamos a escribir de nuevo.</p>
            </div>
            """;
    }
}

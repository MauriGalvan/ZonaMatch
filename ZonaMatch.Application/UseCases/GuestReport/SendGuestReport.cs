using System.Text;
using ZonaMatch.Application.DTOs.GuestReport.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.GuestReport;

// Emails a visitor the PDF report of their analysis, with an invitation to create an account.
// The PDF is built in the browser, so the email text is fixed and only real, small PDFs are attached:
// the endpoint cannot be used to send arbitrary content.
public sealed class SendGuestReport(IEmailService email, GuestReportQuota quota, FrontendLinks links)
{
    public const int MaxReportBytes = 5 * 1024 * 1024;
    public const int MaxEmailsPerRecipientPerDay = 3;
    public const string Subject = "Tu informe de ZonaMatch";
    public const string AttachmentName = "informe-zonamatch.pdf";

    private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

    public async Task ExecuteAsync(SendGuestReportRequest request, byte[]? report, CancellationToken cancellationToken = default)
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
            await email.SendEmailAsync(recipient, Subject, Body(links),
                [new EmailAttachment(AttachmentName, "application/pdf", report)], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ServiceUnavailableException("No pudimos enviar el informe en este momento. Intentá de nuevo en unos minutos.", ex);
        }

        quota.Register(recipient);
    }

    private static string Body(FrontendLinks links) => BrandEmail.Render(new BrandEmailContent(
        Preheader: "Te mandamos el informe de tu análisis de zonas.",
        Title: "¡Gracias por probar ZonaMatch!",
        Paragraphs:
        [
            "Te mandamos adjunto el informe de tu análisis, con las zonas que mejor encajan con lo que te importa.",
            "Con una cuenta podés guardar tus análisis, ajustarlos cuando cambien tus planes, compararlos y buscar zona en grupo con quienes vas a vivir.",
        ],
        Button: new BrandEmailButton("Crear mi cuenta gratis", links.To("/register")),
        Footnote: "Recibiste este mail porque lo pediste desde ZonaMatch. No guardamos tu dirección ni te vamos a volver a escribir."));
}

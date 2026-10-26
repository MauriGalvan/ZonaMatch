namespace ZonaMatch.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);

    // Same email with attached files (for example, the PDF report of an analysis)
    Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default);
}

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

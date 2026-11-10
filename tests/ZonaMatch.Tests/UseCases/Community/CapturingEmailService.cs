using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Tests.UseCases.Community;

// Records every email; recipients listed in FailingRecipients fail as if the SMTP server rejected them.
internal sealed class CapturingEmailService : IEmailService
{
    public List<CapturedEmail> Sent { get; } = [];
    public List<string> FailingRecipients { get; } = [];

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (FailingRecipients.Contains(to))
            return Task.FromException(new InvalidOperationException("The SMTP server is not available."));

        Sent.Add(new CapturedEmail(to, subject, body));
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
        SendEmailAsync(to, subject, body, cancellationToken);
}

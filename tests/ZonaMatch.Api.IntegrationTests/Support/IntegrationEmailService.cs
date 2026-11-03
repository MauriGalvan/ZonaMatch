using System.Collections.Concurrent;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal sealed class IntegrationEmailService : IEmailService
{
    private readonly ConcurrentQueue<IntegrationEmail> _sentEmails = new();

    public IReadOnlyList<IntegrationEmail> SentEmails => _sentEmails.ToArray();

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        _sentEmails.Enqueue(new IntegrationEmail(to, subject, body, []));
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        IReadOnlyList<EmailAttachment> attachments,
        CancellationToken cancellationToken = default)
    {
        _sentEmails.Enqueue(new IntegrationEmail(to, subject, body, attachments));
        return Task.CompletedTask;
    }
}

internal sealed record IntegrationEmail(
    string To,
    string Subject,
    string Body,
    IReadOnlyList<EmailAttachment> Attachments);

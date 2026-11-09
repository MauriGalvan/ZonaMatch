using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Tests.UseCases.Community;

internal sealed class FakeEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

using ZonaMatch.Application.DTOs.Contact.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.Contact;

namespace ZonaMatch.Tests.UseCases.Contact;

public class SendContactMessageTests
{
    private const string TeamEmail = "equipo@example.com";
    private static readonly DateTimeOffset CurrentTime = new(2026, 10, 20, 18, 30, 0, TimeSpan.Zero);

    private readonly FakeEmailService _emailService = new();
    private readonly SendContactMessage _useCase;

    public SendContactMessageTests()
    {
        _useCase = new SendContactMessage(_emailService, new ContactOptions(TeamEmail), new FixedTimeProvider(CurrentTime));
    }

    [Fact]
    public async Task Execute_SendsTheMessageToTheTeamWithSubmittedDetails()
    {
        await _useCase.ExecuteAsync(new SendContactMessageRequest("  ana@example.com ", "  Ana  ", "Caballito\r\no Almagro", null));

        var email = Assert.Single(_emailService.SentEmails);
        Assert.Equal(TeamEmail, email.Recipient);
        Assert.Equal("Formulario de contacto: Ana", email.Subject);
        Assert.Contains("ana@example.com", email.Body);
        Assert.Contains("Caballito o Almagro", email.Body);
        // 18:30 UTC is 15:30 in Argentina.
        Assert.Contains("20/10/2026 15:30", email.Body);
    }

    [Fact]
    public async Task Execute_WhenNameAndZoneAreMissing_ExplainsThatInTheEmail()
    {
        await _useCase.ExecuteAsync(new SendContactMessageRequest("ana@example.com", "  ", null, null));

        var email = Assert.Single(_emailService.SentEmails);
        Assert.Equal("Formulario de contacto: ana@example.com", email.Subject);
        Assert.Contains("<strong>Nombre:</strong> No lo indico", email.Body);
        Assert.Contains("<strong>Dónde busca:</strong> No lo indico", email.Body);
    }

    [Fact]
    public async Task Execute_EncodesSubmittedHtml()
    {
        await _useCase.ExecuteAsync(new SendContactMessageRequest("ana@example.com", "<b>Ana</b>", "<script>alert(1)</script>", null));

        var email = Assert.Single(_emailService.SentEmails);
        Assert.DoesNotContain("<script>", email.Body);
        Assert.Contains("&lt;script&gt;", email.Body);
        Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", email.Body);
    }

    [Fact]
    public async Task Execute_WhenHoneypotIsFilled_DoesNotSendEmail()
    {
        await _useCase.ExecuteAsync(new SendContactMessageRequest("bot@example.com", null, null, "https://spam.example.com"));

        Assert.Empty(_emailService.SentEmails);
    }

    [Fact]
    public async Task Execute_WhenEmailServiceFails_ThrowsServiceUnavailableException()
    {
        _emailService.Failure = new InvalidOperationException("SMTP caido");

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => _useCase.ExecuteAsync(new SendContactMessageRequest("ana@example.com", null, null, null)));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public async Task Execute_WhenEmailSendIsCanceled_PropagatesCancellation()
    {
        _emailService.Failure = new OperationCanceledException("Email send canceled.");

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _useCase.ExecuteAsync(new SendContactMessageRequest("ana@example.com", null, null, null)));

        Assert.Empty(_emailService.SentEmails);
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string Recipient, string Subject, string Body)> SentEmails { get; } = [];
        public Exception? Failure { get; set; }

        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                throw Failure;

            SentEmails.Add((to, subject, body));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
            SendEmailAsync(to, subject, body, cancellationToken);
    }
}

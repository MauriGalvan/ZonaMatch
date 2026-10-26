using System.Text;
using ZonaMatch.Application.DTOs.GuestReports.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.GuestReports;

namespace ZonaMatch.Tests.UseCases.GuestReports;

public class SendGuestReportTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 informe");

    private readonly FakeEmailService _email = new();
    private readonly SendGuestReport _useCase;

    public SendGuestReportTests()
    {
        _useCase = new SendGuestReport(_email, new GuestReportQuota(TimeProvider.System));
    }

    private static SendGuestReportRequest Request(string email = "ana@example.com", string? website = null) =>
        new() { Email = email, Website = website };

    [Fact]
    public async Task Sends_the_pdf_with_a_link_to_register()
    {
        await _useCase.ExecuteAsync(Request(" ana@example.com "), Pdf, "http://localhost:5173/");

        var mail = Assert.Single(_email.Sent);
        Assert.Equal("ana@example.com", mail.To);
        Assert.Equal(SendGuestReport.Subject, mail.Subject);
        Assert.Contains("¡Gracias por probar ZonaMatch!", mail.Body);
        Assert.Contains("http://localhost:5173/register", mail.Body);
        var attachment = Assert.Single(mail.Attachments);
        Assert.Equal(SendGuestReport.AttachmentName, attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(Pdf, attachment.Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>no es un pdf</html>")]
    public async Task Rejects_files_that_are_not_pdf(string content)
    {
        await Assert.ThrowsAsync<BadRequestException>(
            () => _useCase.ExecuteAsync(Request(), Encoding.ASCII.GetBytes(content), "http://localhost:5173"));

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Rejects_a_missing_report()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Request(), null, "http://localhost:5173"));
    }

    [Fact]
    public async Task A_bot_that_fills_the_hidden_field_gets_no_email()
    {
        await _useCase.ExecuteAsync(Request(website: "https://spam.example"), Pdf, "http://localhost:5173");

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Limits_the_reports_per_recipient_and_day()
    {
        for (var i = 0; i < SendGuestReport.MaxEmailsPerRecipientPerDay; i++)
            await _useCase.ExecuteAsync(Request(), Pdf, "http://localhost:5173");

        await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(Request("ANA@example.com"), Pdf, "http://localhost:5173"));
        Assert.Equal(SendGuestReport.MaxEmailsPerRecipientPerDay, _email.Sent.Count);
    }

    [Fact]
    public async Task A_failed_email_is_a_service_unavailable_and_does_not_count()
    {
        _email.Failure = new InvalidOperationException("SMTP caído");

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => _useCase.ExecuteAsync(Request(), Pdf, "http://localhost:5173"));
        Assert.IsType<InvalidOperationException>(error.InnerException);

        _email.Failure = null;
        for (var i = 0; i < SendGuestReport.MaxEmailsPerRecipientPerDay; i++)
            await _useCase.ExecuteAsync(Request(), Pdf, "http://localhost:5173");
        Assert.Equal(SendGuestReport.MaxEmailsPerRecipientPerDay, _email.Sent.Count);
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<(string To, string Subject, string Body, IReadOnlyList<EmailAttachment> Attachments)> Sent { get; } = [];
        public Exception? Failure { get; set; }

        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
            SendEmailAsync(to, subject, body, [], cancellationToken);

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                throw Failure;

            Sent.Add((to, subject, body, attachments));
            return Task.CompletedTask;
        }
    }
}

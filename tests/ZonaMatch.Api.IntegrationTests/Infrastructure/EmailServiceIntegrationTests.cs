using System.Net.Security;
using System.Text;
using FluentAssertions;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Infrastructure.Services;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

[Collection(SmtpIntegrationCollection.Name)]
public sealed class EmailServiceIntegrationTests(Smtp4DevFixture smtp)
{
    [Fact]
    public async Task SendEmailAsync_WhenServerAcceptsStartTls_DeliversHtmlMessage()
    {
        var service = CreateEmailService(smtp);

        await service.SendEmailAsync(
            "recipient@example.test",
            "SMTP integration test",
            "<p>Message delivered through STARTTLS.</p>");

        var message = await smtp.GetLatestMessageAsync();

        message.From.Mailboxes.Should().ContainSingle().Which.Address.Should().Be("sender@example.test");
        message.To.Mailboxes.Should().ContainSingle().Which.Address.Should().Be("recipient@example.test");
        message.Subject.Should().Be("SMTP integration test");
        message.HtmlBody.Should().Contain("Message delivered through STARTTLS.");
    }

    [Fact]
    public async Task SendEmailAsync_WhenAttachmentsAndInlineLogoAreProvided_DeliversMimeParts()
    {
        const string body = $"<img src=\"cid:{BrandEmail.LogoContentId}\"><p>Report attached.</p>";
        const string attachmentText = "integration test report";
        var attachment = new EmailAttachment(
            "report.txt",
            "text/plain",
            Encoding.UTF8.GetBytes(attachmentText));

        await CreateEmailService(smtp).SendEmailAsync(
            "recipient@example.test",
            "SMTP attachment integration test",
            body,
            [attachment]);

        var message = await smtp.GetLatestMessageAsync();

        message.HtmlBody.Should().Contain($"cid:{BrandEmail.LogoContentId}");
        message.BodyParts.OfType<MimePart>()
            .Should().Contain(part => part.ContentId == BrandEmail.LogoContentId);

        var receivedAttachment = message.Attachments.OfType<MimePart>()
            .Should().ContainSingle(part => part.FileName == "report.txt")
            .Which;
        using var content = new MemoryStream();
        receivedAttachment.Content.Should().NotBeNull();
        await receivedAttachment.Content!.DecodeToAsync(content);
        Encoding.UTF8.GetString(content.ToArray()).Should().Be(attachmentText);
    }

    private static EmailService CreateEmailService(Smtp4DevFixture smtp)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:From"] = "sender@example.test",
                ["EmailSettings:Host"] = smtp.Host,
                ["EmailSettings:Port"] = smtp.Port.ToString(),
                ["EmailSettings:User"] = "integration-test-user",
                ["EmailSettings:Password"] = "integration-test-password"
            })
            .Build();

        return new EmailService(configuration, () =>
        {
            var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (_, certificate, _, _) =>
                certificate is not null
                && certificate.GetCertHashString().Equals(smtp.CertificateThumbprint, StringComparison.OrdinalIgnoreCase);
            return client;
        });
    }
}

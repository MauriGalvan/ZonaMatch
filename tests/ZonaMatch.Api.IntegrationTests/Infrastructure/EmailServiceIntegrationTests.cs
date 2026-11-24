using System.Text;
using FluentAssertions;
using MimeKit;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.IntegrationTests.Infrastructure;

[Collection(SmtpIntegrationCollection.Name)]
public sealed class EmailServiceIntegrationTests(Smtp4DevFixture smtp)
{
    [Fact]
    public async Task SendEmailAsync_WhenServerAcceptsStartTls_DeliversHtmlMessage()
    {
        var service = smtp.CreateEmailService();

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

        await smtp.CreateEmailService().SendEmailAsync(
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
}

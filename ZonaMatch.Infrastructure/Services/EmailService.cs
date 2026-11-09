using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using System;
using System.Collections.Generic;
using System.Text;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Services;

public class EmailService : IEmailService
{
    private static readonly Lazy<byte[]?> LogoBytes = new(() =>
    {
        using var stream = typeof(EmailService).Assembly.GetManifestResourceStream("ZonaMatch.EmailLogo.png");
        if (stream is null)
            return null;

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    });

    private readonly IConfiguration _config;
    private readonly Func<SmtpClient> _smtpClientFactory;

    public EmailService(IConfiguration config)
        : this(config, static () => new SmtpClient())
    {
    }

    internal EmailService(IConfiguration config, Func<SmtpClient> smtpClientFactory)
    {
        _config = config;
        _smtpClientFactory = smtpClientFactory;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        SendEmailAsync(to, subject, body, [], cancellationToken);

    public async Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config["EmailSettings:Password"]))
            throw new InvalidOperationException("EmailSettings:Password is not configured (use dotnet user-secrets or an environment variable).");

        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_config["EmailSettings:From"]));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        // Use HTML so email links are rendered correctly in the frontend.
        var builder = new BodyBuilder { HtmlBody = body };

        // Brand layout: the logo travels inside the email (most clients block SVG and remote images)
        if (body.Contains($"cid:{BrandEmail.LogoContentId}", StringComparison.Ordinal) && LogoBytes.Value is { } logo)
        {
            var image = builder.LinkedResources.Add("zonamatch.png", logo, new ContentType("image", "png"));
            image.ContentId = BrandEmail.LogoContentId;
        }
        foreach (var attachment in attachments)
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));

        email.Body = builder.ToMessageBody();

        using var smtp = _smtpClientFactory();

        // Connect to the SMTP server using the appsettings configuration.
        await smtp.ConnectAsync(
            _config["EmailSettings:Host"],
            int.Parse(_config["EmailSettings:Port"]!),
            SecureSocketOptions.StartTls,
            cancellationToken);

        await smtp.AuthenticateAsync(
            _config["EmailSettings:User"],
            _config["EmailSettings:Password"],
            cancellationToken);

        await smtp.SendAsync(email, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}

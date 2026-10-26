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

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        SendEmailAsync(to, subject, body, [], cancellationToken);

    public async Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(_config["EmailSettings:From"]));
        email.To.Add(MailboxAddress.Parse(to));
        email.Subject = subject;

        // Usamos formato Html para poder mandar links bonitos al frontend
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

        using var smtp = new SmtpClient();

        // Nos conectamos al servidor SMTP usando los datos del appsettings
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

using Microsoft.Extensions.Logging.Abstractions;
using ZonaMatch.Application.DTOs.PasswordResets.Requests;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.PasswordResets;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.PasswordResets;

public class PasswordResetTests
{
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 26, 15, 0, 0, TimeSpan.Zero));
    private readonly Usuario _user = new() { Email = "ana@example.com", PasswordHash = "hash:vieja" };
    private readonly FakeUsers _users = new();
    private readonly FakeResets _resets;
    private readonly FakeEmail _email = new();
    private readonly RequestPasswordReset _request;
    private readonly CompletePasswordReset _complete;

    public PasswordResetTests()
    {
        _users.All.Add(_user);
        _resets = new FakeResets(_users);
        _request = new RequestPasswordReset(_users, _resets, _email, new FrontendLinks("http://localhost:5173/"), _clock, NullLogger<RequestPasswordReset>.Instance);
        _complete = new CompletePasswordReset(_resets, new FakeHasher(), _clock, NullLogger<CompletePasswordReset>.Instance);
    }

    private Task Ask(string email = "ana@example.com") => _request.ExecuteAsync(new RequestPasswordResetRequest { Email = email });

    private Task Choose(string token, string password = "nueva-clave") =>
        _complete.ExecuteAsync(new CompletePasswordResetRequest { Token = token, Password = password, ConfirmPassword = password });

    // The token travels only in the emailed link
    private string TokenFromEmail()
    {
        var body = _email.Sent[^1].Body;
        var start = body.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        return body[start..body.IndexOf('"', start)];
    }

    [Fact]
    public async Task Emails_a_link_with_the_brand_template_and_stores_only_the_hash()
    {
        await Ask(" ANA@example.com ");

        var mail = Assert.Single(_email.Sent);
        Assert.Equal("ana@example.com", mail.To);
        Assert.Equal(RequestPasswordReset.Subject, mail.Subject);
        Assert.Contains("http://localhost:5173/restablecer-contrasena?token=", mail.Body);
        Assert.Contains($"cid:{BrandEmail.LogoContentId}", mail.Body);
        var stored = Assert.Single(_resets.Tokens);
        Assert.Equal(PasswordResetTokens.Hash(TokenFromEmail()), stored.TokenHash);
        Assert.NotEqual(TokenFromEmail(), stored.TokenHash);
        Assert.Equal(_clock.GetUtcNow().AddHours(1), stored.ExpiresAt);
    }

    [Fact]
    public async Task An_unknown_email_gets_the_same_answer_and_no_email()
    {
        await Ask("nadie@example.com");

        Assert.Empty(_email.Sent);
        Assert.Empty(_resets.Tokens);
    }

    [Fact]
    public async Task Sends_at_most_three_links_per_hour()
    {
        for (var i = 0; i < 5; i++)
            await Ask();

        Assert.Equal(RequestPasswordReset.MaxLinksPerHour, _email.Sent.Count);
        _clock.Now = _clock.Now.AddMinutes(61);
        await Ask();
        Assert.Equal(RequestPasswordReset.MaxLinksPerHour + 1, _email.Sent.Count);
    }

    [Fact]
    public async Task A_failed_email_does_not_reveal_that_the_account_exists()
    {
        _email.Failure = new InvalidOperationException("SMTP caído");

        await Ask();
    }

    [Fact]
    public async Task The_link_changes_the_password_once_and_cancels_the_other_links()
    {
        await Ask();
        var first = TokenFromEmail();
        await Ask();
        var second = TokenFromEmail();

        await Choose(second);

        Assert.Equal("hash:nueva-clave", _user.PasswordHash);
        Assert.All(_resets.Tokens, token => Assert.NotNull(token.UsedAt));
        await Assert.ThrowsAsync<BadRequestException>(() => Choose(second, "otra-clave"));
        await Assert.ThrowsAsync<BadRequestException>(() => Choose(first, "otra-clave"));
        Assert.Equal("hash:nueva-clave", _user.PasswordHash);
    }

    [Fact]
    public async Task An_expired_or_unknown_link_is_rejected()
    {
        await Ask();
        var token = TokenFromEmail();
        _clock.Now = _clock.Now.Add(PasswordResetTokens.Lifetime);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Choose(token));
        Assert.Equal(CompletePasswordReset.InvalidLinkMessage, error.Message);
        await Assert.ThrowsAsync<BadRequestException>(() => Choose("no-existe"));
        Assert.Equal("hash:vieja", _user.PasswordHash);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string? hash, string password) => hash == Hash(password);
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<Usuario> All { get; } = [];
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(All.Any(u => u.Email == email));
        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(All.FirstOrDefault(u => u.Email == email));
        public Task AddAsync(Usuario user, CancellationToken cancellationToken = default) { All.Add(user); return Task.CompletedTask; }
    }

    private sealed class FakeResets(FakeUsers users) : IPasswordResetRepository
    {
        public List<PasswordResetToken> Tokens { get; } = [];

        public Task<int> CountCreatedSinceAsync(Guid userId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens.Count(t => t.UserId == userId && t.CreatedAt >= since));

        public Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default) { Tokens.Add(token); return Task.CompletedTask; }

        public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task ResetPasswordAsync(PasswordResetToken token, string passwordHash, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            if (token.UsedAt is not null)
                throw new BadRequestException(CompletePasswordReset.InvalidLinkMessage);

            users.All.Single(u => u.Id == token.UserId).PasswordHash = passwordHash;
            foreach (var pending in Tokens.Where(t => t.UserId == token.UserId && t.UsedAt is null))
                pending.UsedAt = now;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public Exception? Failure { get; set; }

        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                throw Failure;
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string to, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default) =>
            SendEmailAsync(to, subject, body, cancellationToken);
    }
}

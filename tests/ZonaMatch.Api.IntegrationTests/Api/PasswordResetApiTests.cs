using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.DTOs.PasswordReset.Requests;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.UseCases.PasswordReset;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class PasswordResetApiTests(PostgreSqlDatabaseFixture database)
{
    private const string CurrentPassword = "IntegrationPassword123!";
    private const string NewPassword = "NewIntegrationPassword456!";

    [Fact]
    public async Task PasswordResetRequest_WhenEmailExistsOrDoesNotExist_ReturnsSameResponseAndOnlyEmailsTheExistingUser()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"password-reset-{Guid.NewGuid():N}@example.com";
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, CurrentPassword, CurrentPassword, new DateOnly(1990, 5, 31)));
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await registrationResponse.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();

        var emailService = GetEmailService(factory);
        var existingEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/password-resets",
            new RequestPasswordResetRequest { Email = email.ToUpperInvariant() });
        var unknownEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/password-resets",
            new RequestPasswordResetRequest { Email = $"missing-{Guid.NewGuid():N}@example.com" });

        existingEmailResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        unknownEmailResponse.StatusCode.Should().Be(existingEmailResponse.StatusCode);
        (await existingEmailResponse.Content.ReadAsStringAsync()).Should()
            .Be(await unknownEmailResponse.Content.ReadAsStringAsync());
        emailService.SentEmails.Should().ContainSingle();
        emailService.SentEmails.Single().To.Should().Be(email);

        var token = ExtractToken(emailService.SentEmails.Single().Body);
        await using var verificationContext = database.CreateDbContext();
        var savedToken = await verificationContext.PasswordResetTokens.SingleAsync(item => item.UserId == user!.Id);
        savedToken.TokenHash.Should().Be(PasswordResetTokens.Hash(token));
        savedToken.TokenHash.Should().NotBe(token);
    }

    [Fact]
    public async Task PasswordReset_WhenTokenIsUsed_ChangesPasswordOnceAndInvalidatesOtherPendingLinks()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"password-reset-complete-{Guid.NewGuid():N}@example.com";
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, CurrentPassword, CurrentPassword, new DateOnly(1990, 5, 31)));
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await registrationResponse.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();

        var emailService = GetEmailService(factory);
        var firstRequestResponse = await RequestResetAsync(client, email);
        firstRequestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var secondRequestResponse = await RequestResetAsync(client, email);
        secondRequestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        emailService.SentEmails.Should().HaveCount(2);
        var firstToken = ExtractToken(emailService.SentEmails[0].Body);
        var secondToken = ExtractToken(emailService.SentEmails[1].Body);

        var completionResponse = await CompleteResetAsync(client, firstToken, NewPassword);
        completionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var repeatedTokenResponse = await CompleteResetAsync(client, firstToken, "AnotherPassword789!");
        var otherPendingTokenResponse = await CompleteResetAsync(client, secondToken, "AnotherPassword789!");
        repeatedTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        otherPendingTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, CurrentPassword));
        var newPasswordLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, NewPassword));
        oldPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verificationContext = database.CreateDbContext();
        var tokens = await verificationContext.PasswordResetTokens
            .Where(item => item.UserId == user!.Id)
            .ToListAsync();
        tokens.Should().HaveCount(2).And.OnlyContain(item => item.UsedAt.HasValue);
    }

    [Fact]
    public async Task PasswordResetConfirmation_WhenTokenIsUnknownOrExpired_ReturnsSameBadRequest()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"password-reset-expired-{Guid.NewGuid():N}@example.com";
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, CurrentPassword, CurrentPassword, new DateOnly(1990, 5, 31)));
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await registrationResponse.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();

        const string expiredToken = "expired-integration-reset-token";
        await using (var seedContext = database.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            seedContext.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user!.Id,
                TokenHash = PasswordResetTokens.Hash(expiredToken),
                CreatedAt = now.AddHours(-2),
                ExpiresAt = now.AddHours(-1)
            });
            await seedContext.SaveChangesAsync();
        }

        var unknownTokenResponse = await CompleteResetAsync(client, "unknown-integration-reset-token", NewPassword);
        var expiredTokenResponse = await CompleteResetAsync(client, expiredToken, NewPassword);

        unknownTokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        expiredTokenResponse.StatusCode.Should().Be(unknownTokenResponse.StatusCode);
        var unknownProblem = await unknownTokenResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var expiredProblem = await expiredTokenResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        unknownProblem.Should().NotBeNull();
        expiredProblem.Should().NotBeNull();
        unknownProblem!.Title.Should().Be(CompletePasswordReset.InvalidLinkMessage);
        expiredProblem!.Title.Should().Be(unknownProblem.Title);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private static IntegrationEmailService GetEmailService(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IEmailService>().Should().BeOfType<IntegrationEmailService>().Subject;

    private static Task<HttpResponseMessage> RequestResetAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/v1/password-resets", new RequestPasswordResetRequest { Email = email });

    private static Task<HttpResponseMessage> CompleteResetAsync(HttpClient client, string token, string password) =>
        client.PostAsJsonAsync("/api/v1/password-resets/confirmation", new CompletePasswordResetRequest
        {
            Token = token,
            Password = password,
            ConfirmPassword = password
        });

    private static string ExtractToken(string emailBody)
    {
        var match = Regex.Match(emailBody, @"restablecer-contrasena\?token=([A-Za-z0-9_-]+)");
        match.Success.Should().BeTrue("the reset email should contain its one-time link");
        return match.Groups[1].Value;
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;

namespace ZonaMatch.Api.IntegrationTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class AuthApiTests(PostgreSqlDatabaseFixture database)
{
    private const string ValidPassword = "IntegrationPassword123!";

    [Fact]
    public async Task RegisterAndLogin_WhenCredentialsAreValid_AuthenticatesCurrentSessionWithCookie()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        var email = $"integration-{Guid.NewGuid():N}@example.com";
        const string password = "IntegrationPassword123!";

        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, password, password, new DateOnly(1990, 5, 31)));

        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registeredUser = await registrationResponse.Content.ReadFromJsonAsync<UserResponse>();
        registeredUser.Should().NotBeNull();
        registeredUser!.Email.Should().Be(email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var setCookie = loginResponse.Headers.GetValues("Set-Cookie").Single();
        setCookie.Should().Contain("__Host-zonamatch-auth=")
            .And.ContainEquivalentOf("HttpOnly")
            .And.ContainEquivalentOf("Secure");

        var sessionResponse = await client.GetAsync("/api/v1/auth/me");
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await sessionResponse.Content.ReadFromJsonAsync<CurrentSessionResponse>();
        session.Should().NotBeNull();
        session!.Id.Should().Be(registeredUser.Id);
        session.Email.Should().Be(email);
        session.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsConflictWithoutCreatingDuplicate()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, ValidPassword, ValidPassword, new DateOnly(1990, 5, 31));

        var firstResponse = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateResponse = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Users.CountAsync(user => user.Email == email)).Should().Be(1);
    }

    [Fact]
    public async Task Login_WhenEmailDoesNotExistOrPasswordIsWrong_ReturnsSameUnauthorizedMessage()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"wrong-password-{Guid.NewGuid():N}@example.com";
        var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, ValidPassword, ValidPassword, new DateOnly(1990, 5, 31)));
        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var unknownEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest($"missing-{Guid.NewGuid():N}@example.com", ValidPassword));
        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "DifferentPassword123!"));

        unknownEmailResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unknownEmailProblem = await unknownEmailResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        var wrongPasswordProblem = await wrongPasswordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        unknownEmailProblem.Should().NotBeNull();
        wrongPasswordProblem.Should().NotBeNull();
        unknownEmailProblem!.Title.Should().Be(wrongPasswordProblem!.Title);
        unknownEmailProblem.Title.Should().Be("Email o contraseña incorrectos.");
    }

    [Fact]
    public async Task Register_WhenRequestIsInvalid_ReturnsValidationErrorsWithoutCreatingUser()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var invalidEmail = $"invalid-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(invalidEmail, "short", "different", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Users.CountAsync(user => user.Email == invalidEmail)).Should().Be(0);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
}

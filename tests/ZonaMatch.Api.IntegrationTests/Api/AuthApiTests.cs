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
            new RegisterRequest(email, password, password, new DateOnly(1990, 5, 31), "  María José  ", "  De la Peña  "));

        registrationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var registeredUser = await registrationResponse.Content.ReadFromJsonAsync<UserResponse>();
        registeredUser.Should().NotBeNull();
        registeredUser!.Email.Should().Be(email);
        registeredUser.FirstName.Should().Be("María José");
        registeredUser.LastName.Should().Be("De la Peña");

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login!.User.FirstName.Should().Be("María José");
        login.User.LastName.Should().Be("De la Peña");
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
        session.FirstName.Should().Be("María José");
        session.LastName.Should().Be("De la Peña");
    }

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ReturnsConflictWithoutCreatingDuplicate()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, ValidPassword, ValidPassword, new DateOnly(1990, 5, 31), "Ana", "Pérez");

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
            new RegisterRequest(email, ValidPassword, ValidPassword, new DateOnly(1990, 5, 31), "Ana", "Pérez"));
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
            new RegisterRequest(invalidEmail, "short", "different", null, "Ana", "Pérez"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Errors.Should().NotBeEmpty();
        problem.Title.Should().Be("Revisá los datos enviados.");
        problem.Errors.SelectMany(field => field.Value).Should().Contain("El email no tiene un formato válido.");

        await using var dbContext = database.CreateDbContext();
        (await dbContext.Users.CountAsync(user => user.Email == invalidEmail)).Should().Be(0);
    }

    [Fact]
    public async Task Register_WhenBodyHasAValueOfTheWrongType_ReturnsTheValidationErrorInSpanish()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        using var body = new StringContent("""{ "email": 123, "password": true }""", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Title.Should().Be("Revisá los datos enviados.");
        problem.Errors.SelectMany(field => field.Value).Should().OnlyContain(message => !message.Contains("JSON") && !message.StartsWith("The "));
    }

    [Theory]
    [InlineData(null, "Pérez", "El nombre es obligatorio.")]
    [InlineData("Ana", "   ", "El apellido es obligatorio.")]
    public async Task Register_WhenNameIsMissing_ReturnsValidationError(string? firstName, string? lastName, string message)
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);
        var email = $"integration-{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest(email, ValidPassword, ValidPassword, new DateOnly(1990, 5, 31), firstName, lastName);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.SelectMany(field => field.Value).Should().Contain(message);
    }

    [Fact]
    public async Task CompleteName_WhenCurrentAccountHasNoName_PersistsBothNamesForThatAccount()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var legacyUser = await TestApiUser.RegisterAsync(factory);
        using var otherUser = await TestApiUser.RegisterAsync(factory);
        await using var dbContext = database.CreateDbContext();
        await dbContext.Users.Where(user => user.Id == legacyUser.User.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.FirstName, (string?)null)
                .SetProperty(user => user.LastName, (string?)null));

        var sessionBefore = await legacyUser.Client.GetFromJsonAsync<CurrentSessionResponse>("/api/v1/auth/me");
        var response = await legacyUser.Client.PutAsJsonAsync(
            "/api/v1/auth/me/name", new CompleteUserNameRequest("  José Luis  ", "  Ávila  "));
        var sessionAfter = await legacyUser.Client.GetFromJsonAsync<CurrentSessionResponse>("/api/v1/auth/me");
        var otherSession = await otherUser.Client.GetFromJsonAsync<CurrentSessionResponse>("/api/v1/auth/me");

        sessionBefore!.FirstName.Should().BeNull();
        sessionBefore.LastName.Should().BeNull();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedSession = await response.Content.ReadFromJsonAsync<CurrentSessionResponse>();
        updatedSession!.FirstName.Should().Be("José Luis");
        updatedSession.LastName.Should().Be("Ávila");
        sessionAfter.Should().Be(updatedSession);
        otherSession!.FirstName.Should().Be(otherUser.User.FirstName);
        otherSession.LastName.Should().Be(otherUser.User.LastName);
    }

    [Fact]
    public async Task CompleteName_WhenCurrentAccountAlreadyHasAName_ReturnsConflictAndPreservesName()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var registeredUser = await TestApiUser.RegisterAsync(factory);

        var response = await registeredUser.Client.PutAsJsonAsync(
            "/api/v1/auth/me/name", new CompleteUserNameRequest("José", "Ávila"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var session = await registeredUser.Client.GetFromJsonAsync<CurrentSessionResponse>("/api/v1/auth/me");
        session!.FirstName.Should().Be(registeredUser.User.FirstName);
        session.LastName.Should().Be(registeredUser.User.LastName);
    }

    [Fact]
    public async Task CompleteName_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new ZonaMatchApiFactory(database.ConnectionString);
        using var client = CreateClient(factory);

        var response = await client.PutAsJsonAsync("/api/v1/auth/me/name", new CompleteUserNameRequest("Ana", "Pérez"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
}

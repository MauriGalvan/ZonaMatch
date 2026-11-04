using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Support;

internal sealed class TestApiUser(HttpClient client, UserResponse user) : IDisposable
{
    private const string Password = "IntegrationPassword123!";

    public HttpClient Client { get; } = client;
    public UserResponse User { get; } = user;

    public static async Task<TestApiUser> RegisterAsync(
        WebApplicationFactory<Program> factory,
        CancellationToken cancellationToken = default)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
        var email = $"integration-{Guid.NewGuid():N}@example.com";

        var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, Password, Password, new DateOnly(1990, 5, 31)),
            cancellationToken);

        if (registration.StatusCode != HttpStatusCode.Created)
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Integration user registration failed with {(int)registration.StatusCode}: {await registration.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var user = await registration.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);
        if (user is null)
        {
            client.Dispose();
            throw new InvalidOperationException("Integration user registration returned an empty response.");
        }

        // Avoid using the rate-limited login endpoint during fixture setup. The authentication
        // integration test covers login and cookies; these workflows need only an authenticated client.
        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var (token, _) = tokenService.Generate(new User
        {
            Id = user.Id,
            Email = user.Email,
            IsAdministrator = user.IsAdmin
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestApiUser(client, user);
    }

    public void AuthenticateAsAdministrator(WebApplicationFactory<Program> factory)
    {
        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var (token, _) = tokenService.Generate(new User
        {
            Id = User.Id,
            Email = User.Email,
            IsAdministrator = true
        });

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose() => Client.Dispose();
}

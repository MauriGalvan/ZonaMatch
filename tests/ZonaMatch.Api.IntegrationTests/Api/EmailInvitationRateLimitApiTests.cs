using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZonaMatch.Api.Background;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.DTOs.GroupInvitation.Requests;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Api.IntegrationTests.Api;

public sealed class EmailInvitationRateLimitApiTests
{
    [Fact]
    public async Task CreateEmailInvitation_WhenUsersShareAnIpAddress_EnforcesSeparateLimits()
    {
        using var baseFactory = new ZonaMatchApiFactory("Host=127.0.0.1;Database=unused");
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimits:InvitationEmail:PermitLimit"] = "1"
                }));

            builder.ConfigureTestServices(services =>
            {
                var purgeService = services.Single(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(PurgeExpiredAnalysesBackgroundService));
                services.Remove(purgeService);
            });
        });

        using var client = new HttpClient(factory.Server.CreateHandler(context =>
            context.Connection.RemoteIpAddress = IPAddress.Loopback))
        {
            BaseAddress = new Uri("https://localhost")
        };
        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var (firstToken, _) = tokenService.Generate(new User { Id = Guid.NewGuid(), Email = "first@example.com" });
        var (secondToken, _) = tokenService.Generate(new User { Id = Guid.NewGuid(), Email = "second@example.com" });

        (await SendInvalidInvitationAsync(client, firstToken)).Should().Be(HttpStatusCode.BadRequest);
        (await SendInvalidInvitationAsync(client, firstToken)).Should().Be(HttpStatusCode.TooManyRequests);
        (await SendInvalidInvitationAsync(client, secondToken)).Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpStatusCode> SendInvalidInvitationAsync(HttpClient client, string token)
    {
        // Model validation confirms that the request passed the limiter without creating an invitation.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/group-invitations/email")
        {
            Content = JsonContent.Create(new CreateInvitationRequest())
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

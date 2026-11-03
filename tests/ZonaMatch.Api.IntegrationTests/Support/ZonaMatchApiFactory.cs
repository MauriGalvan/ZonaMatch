using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Api.Background;

namespace ZonaMatch.Api.IntegrationTests.Support;

public sealed class ZonaMatchApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-not-a-real-secret",
                ["Jwt:Issuer"] = "ZonaMatch.IntegrationTests",
                ["Jwt:Audience"] = "ZonaMatch.IntegrationTests",
                ["Jwt:ExpirationMinutes"] = "10"
            }));

        builder.ConfigureTestServices(services =>
        {
            var warmProfilesService = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(WarmZoneProfilesService));

            if (warmProfilesService is not null)
                services.Remove(warmProfilesService);

            // The integration database contains application data, not the external OSM/geo dump.
            // Keep the HTTP/use-case/persistence flow real while providing one known test zone.
            services.RemoveAll<IZoneRepository>();
            services.AddSingleton<IZoneRepository, IntegrationZoneRepository>();

            // Integration tests verify application notifications without sending external emails.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, IntegrationEmailService>();

            // Scoring depends on imported POI, transit, and market datasets; persistence is tested here
            // without requiring those external datasets to be imported into the temporary database.
            services.RemoveAll<IAnalysisResultCalculator>();
            services.AddScoped<IAnalysisResultCalculator, IntegrationAnalysisResultCalculator>();

            // Group ranking consumes the same zone profiles and routing client as the production flow.
            // Use deterministic in-memory zone metrics and route durations in this isolated test host.
            services.RemoveAll<IZoneProfilesRepository>();
            services.AddSingleton<IZoneProfilesRepository, IntegrationZoneProfilesRepository>();
            services.RemoveAll<IRoutingClient>();
            services.AddSingleton<IRoutingClient, IntegrationRoutingClient>();
        });
    }
}

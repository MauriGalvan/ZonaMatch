using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ZonaMatch.Api.IntegrationTests.Support;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.E2E.MailHost;

internal static class MailHost
{
    public static async Task Main()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
        if (connection.Database != "zonamatch_e2e" || connection.Host != "127.0.0.1" || connection.Port != 55433)
            throw new InvalidOperationException("The E2E mail host requires the disposable local PostGIS database.");

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        await using var inbox = new Smtp4DevFixture();
        await inbox.InitializeAsync();
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(inbox.CreateEmailService());
                services.AddSingleton<IStartupFilter>(new InboxEndpoint(inbox));
            });
        });
        factory.UseKestrel(options => options.ListenLocalhost(5289));
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health", shutdown.Token);
        health.EnsureSuccessStatusCode();
        Console.WriteLine("The E2E mail host is ready on http://localhost:5289.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
    }

    private sealed class InboxEndpoint(Smtp4DevFixture inbox) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/e2e/inbox/latest", branch => branch.Run(async context =>
            {
                var message = await inbox.GetLatestMessageAsync();
                await context.Response.WriteAsJsonAsync(new
                {
                    message.Subject,
                    Recipients = message.To.Mailboxes.Select(mailbox => mailbox.Address),
                    Html = message.HtmlBody
                }, context.RequestAborted);
            }));
            next(app);
        };
    }
}

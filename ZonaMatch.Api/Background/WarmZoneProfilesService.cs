using System.Diagnostics;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Background;

// Loads zone profiles during startup so the first scoring request does not pay the cache initialization cost.
public sealed class WarmZoneProfilesService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WarmZoneProfilesService> _logger;

    public WarmZoneProfilesService(IServiceScopeFactory scopeFactory, ILogger<WarmZoneProfilesService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            using var scope = _scopeFactory.CreateScope();
            var profiles = await scope.ServiceProvider.GetRequiredService<IZoneProfilesRepository>().ListAsync(stoppingToken);
            _logger.LogInformation(
                "Loaded scoring profiles for {Count} zones in {Seconds:F1} seconds.",
                profiles.Count,
                stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not warm the zone profile cache; it will be loaded by the first request.");
        }
    }
}

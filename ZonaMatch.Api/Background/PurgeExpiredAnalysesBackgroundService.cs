using ZonaMatch.Application.UseCases.SavedAnalysis;

namespace ZonaMatch.Api.Background;

public sealed class PurgeExpiredAnalysesBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PurgeExpiredAnalysesBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PurgeAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await PurgeAsync(stoppingToken);
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var purgeExpiredAnalyses = scope.ServiceProvider.GetRequiredService<PurgeExpiredAnalyses>();
            var purgedCount = await purgeExpiredAnalyses.ExecuteAsync(cancellationToken);

            if (purgedCount > 0)
                logger.LogInformation("Permanently deleted {Count} expired saved analyses.", purgedCount);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not purge expired saved analyses.");
        }
    }
}

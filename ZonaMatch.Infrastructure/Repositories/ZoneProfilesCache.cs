using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Infrastructure.Repositories;

// Keeps profiles for all zones in memory so concurrent requests share one refresh.
public sealed class ZoneProfilesCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeProvider _timeProvider;

    private IReadOnlyList<ZoneProfile>? _profiles;
    private DateTimeOffset _expiresAt;

    public ZoneProfilesCache(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ZoneProfile>> GetAsync(
        Func<CancellationToken, Task<IReadOnlyList<ZoneProfile>>> loadProfiles,
        CancellationToken cancellationToken)
    {
        if (GetCurrentProfiles() is { } profiles)
            return profiles;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have refreshed the cache while this request was waiting.
            if (GetCurrentProfiles() is { } currentProfiles)
                return currentProfiles;

            var refreshedProfiles = await loadProfiles(cancellationToken);
            if (refreshedProfiles.Count > 0)
            {
                _profiles = refreshedProfiles;
                _expiresAt = _timeProvider.GetUtcNow() + Lifetime;
            }

            return refreshedProfiles;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private IReadOnlyList<ZoneProfile>? GetCurrentProfiles() =>
        _profiles is not null && _timeProvider.GetUtcNow() < _expiresAt ? _profiles : null;
}

using ZonaMatch.Domain.Scoring;

namespace ZonaMatch.Infrastructure.Repositories;

// Keeps the profiles of every zone in this instance's memory, and concurrent requests share one load. It is not in
// HybridCache: HybridCache serializes each value to measure it, and these profiles (several MB, with dictionaries keyed by
// type and rooms) exceed its payload limit; every instance can rebuild them from the database.
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

    // An empty result (data not loaded yet) is not kept, so the next request tries again.
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

    // After loading data with the ETL: the next request rebuilds the profiles.
    public void Invalidate() => _profiles = null;

    private IReadOnlyList<ZoneProfile>? GetCurrentProfiles() =>
        _profiles is not null && _timeProvider.GetUtcNow() < _expiresAt ? _profiles : null;
}

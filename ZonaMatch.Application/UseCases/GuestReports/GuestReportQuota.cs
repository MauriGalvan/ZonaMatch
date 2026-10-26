using System.Collections.Concurrent;

namespace ZonaMatch.Application.UseCases.GuestReports;

// Reports emailed to each address today (Argentina time). Kept in memory: a restart resets it,
// which is fine for a limit meant to stop someone from flooding an inbox.
public sealed class GuestReportQuota(TimeProvider clock)
{
    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    private readonly ConcurrentDictionary<string, (DateOnly Day, int Count)> _sent = new();

    public int SentToday(string recipient) =>
        _sent.TryGetValue(Key(recipient), out var entry) && entry.Day == Today() ? entry.Count : 0;

    public void Register(string recipient)
    {
        var today = Today();
        _sent.AddOrUpdate(Key(recipient), (today, 1), (_, entry) => entry.Day == today ? (today, entry.Count + 1) : (today, 1));

        // Addresses from previous days no longer count
        foreach (var (key, entry) in _sent)
            if (entry.Day != today)
                _sent.TryRemove(key, out _);
    }

    private static string Key(string recipient) => recipient.Trim().ToLowerInvariant();

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(ArgentinaOffset).DateTime);
}

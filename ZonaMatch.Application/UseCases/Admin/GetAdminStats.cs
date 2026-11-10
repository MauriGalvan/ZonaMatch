using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;

namespace ZonaMatch.Application.UseCases.Admin;

// Site statistics for the admin panel: users, activity, community, and the most analyzed and saved zones.
public sealed class GetAdminStats(IAdminStatsRepository stats, TimeProvider timeProvider)
{
    public const int WeekCount = 12;
    public const int TopZoneCount = 5;

    // Argentina has no daylight saving time, so a fixed offset is enough to group by local week.
    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    public async Task<AdminStatsResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var weekStarts = GetWeekStarts(now);
        // Npgsql only writes DateTimeOffset values in UTC.
        var registrationsSince = new DateTimeOffset(weekStarts[0].ToDateTime(TimeOnly.MinValue), ArgentinaOffset).ToUniversalTime();

        var snapshot = await stats.GetSnapshotAsync(
            new AdminStatsPeriod(now.AddDays(-7), now.AddDays(-30), registrationsSince, TopZoneCount),
            cancellationToken);

        return new AdminStatsResponse(
            snapshot.Users,
            snapshot.Activity,
            snapshot.Community,
            CountByWeek(snapshot.RecentRegistrationDates, weekStarts),
            snapshot.TopAnalyzedZones,
            snapshot.TopSavedZones,
            snapshot.CriteriaUsage,
            snapshot.Operations,
            now);
    }

    // The Mondays of the last WeekCount weeks, including the current one, oldest first.
    private static List<DateOnly> GetWeekStarts(DateTimeOffset now)
    {
        var currentWeekStart = StartOfWeek(DateOnly.FromDateTime(now.ToOffset(ArgentinaOffset).DateTime));
        return Enumerable.Range(0, WeekCount)
            .Select(index => currentWeekStart.AddDays(-7 * (WeekCount - 1 - index)))
            .ToList();
    }

    private static List<AdminWeeklyCount> CountByWeek(IReadOnlyList<DateTimeOffset> dates, List<DateOnly> weekStarts)
    {
        var counts = dates
            .Select(date => StartOfWeek(DateOnly.FromDateTime(date.ToOffset(ArgentinaOffset).DateTime)))
            .GroupBy(weekStart => weekStart)
            .ToDictionary(group => group.Key, group => group.Count());

        return weekStarts.Select(weekStart => new AdminWeeklyCount(weekStart, counts.GetValueOrDefault(weekStart))).ToList();
    }

    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

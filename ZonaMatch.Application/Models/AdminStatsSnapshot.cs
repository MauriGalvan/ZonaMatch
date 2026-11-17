using ZonaMatch.Application.DTOs.Admin.Responses;

namespace ZonaMatch.Application.Models;

// Raw statistics read by IAdminStatsRepository. GetAdminStats groups the registration dates into weeks.
public sealed record AdminStatsSnapshot(
    AdminUserStats Users,
    AdminActivityStats Activity,
    AdminCommunityStats Community,
    IReadOnlyList<DateTimeOffset> RecentRegistrationDates,
    IReadOnlyList<AdminZoneCount> TopAnalyzedZones,
    IReadOnlyList<AdminZoneCount> TopSavedZones,
    IReadOnlyList<AdminCriterionCount> CriteriaUsage,
    AdminOperationSplit Operations);

// Time window for the statistics, computed by the use case from the current time.
public sealed record AdminStatsPeriod(DateTimeOffset SinceLastDay, DateTimeOffset Since7Days, DateTimeOffset Since30Days, DateTimeOffset RegistrationsSince, int TopZoneCount);

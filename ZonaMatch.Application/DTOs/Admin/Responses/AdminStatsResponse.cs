namespace ZonaMatch.Application.DTOs.Admin.Responses;

// GET /api/v1/admin/stats: site statistics read from the database for the admin panel.
public sealed record AdminStatsResponse(
    AdminUserStats Users,
    AdminActivityStats Activity,
    AdminCommunityStats Community,
    // New registrations per week, oldest first; weeks start on Monday (Argentina time).
    IReadOnlyList<AdminWeeklyCount> WeeklyRegistrations,
    IReadOnlyList<AdminZoneCount> TopAnalyzedZones,
    IReadOnlyList<AdminZoneCount> TopSavedZones,
    // Criteria used in saved analyses, most used first.
    IReadOnlyList<AdminCriterionCount> CriteriaUsage,
    // Saved analyses by housing operation (alquiler / compra).
    AdminOperationSplit Operations,
    DateTimeOffset GeneratedAt);

public sealed record AdminUserStats(
    int Total,
    int NewLast7Days,
    int NewLast30Days,
    int Administrators,
    // Users who completed their profile.
    int WithProfile,
    // Users who saved at least one analysis.
    int WithAnalysis);

public sealed record AdminActivityStats(
    int Analyses,
    int AnalysesLast30Days,
    int FavoriteAnalyses,
    int Groups,
    int GroupsLast30Days,
    int GroupInvitations,
    int AcceptedGroupInvitations,
    int SavedZones,
    // PDF reports emailed to guests (visitors without an account).
    int GuestReports,
    int GuestReportsLast30Days);

public sealed record AdminCommunityStats(
    int Reviews,
    int ReviewsLast30Days,
    // Average overall score from 1 to 5; null when there are no reviews.
    double? AverageReviewScore,
    int Questions,
    int UnansweredQuestions,
    int Answers,
    int PendingContributions,
    int ApprovedContributions,
    int RejectedContributions,
    // Points of interest added or corrected by the community and still visible.
    int CommunityPois);

public sealed record AdminWeeklyCount(DateOnly WeekStart, int Count);

public sealed record AdminZoneCount(string ZoneSlug, string Name, int Count);

public sealed record AdminCriterionCount(string CriterionId, string Name, int Count, int HighPriorityCount);

public sealed record AdminOperationSplit(int Rent, int Purchase);

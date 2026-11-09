using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.DTOs.Admin.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Domain.Enums;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories;

// Counts over the application tables. The queries run one after another because a DbContext is not thread-safe.
public sealed class AdminStatsRepository(ZonaMatchDbContext dbContext) : IAdminStatsRepository
{
    private const string HighPriority = "alta";
    private const string PurchaseOperation = "compra";

    public async Task<AdminStatsSnapshot> GetSnapshotAsync(AdminStatsPeriod period, CancellationToken cancellationToken = default)
    {
        var users = await GetUserStatsAsync(period, cancellationToken);
        var activity = await GetActivityStatsAsync(period, cancellationToken);
        var community = await GetCommunityStatsAsync(period, cancellationToken);

        var recentRegistrationDates = await dbContext.Users.AsNoTracking()
            .Where(user => user.CreatedAt >= period.RegistrationsSince)
            .Select(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

        // EF cannot order by the members of a record built in the query, so the rows are projected to an
        // anonymous type, ordered and limited in SQL, and mapped to records in memory.
        var topAnalyzedZones = (await dbContext.AnalysisZones.AsNoTracking()
                .GroupBy(zone => zone.ZoneSlug)
                .Select(group => new { Slug = group.Key, Name = group.Max(zone => zone.Name), Count = group.Count() })
                .OrderByDescending(zone => zone.Count)
                .ThenBy(zone => zone.Slug)
                .Take(period.TopZoneCount)
                .ToListAsync(cancellationToken))
            .Select(zone => new AdminZoneCount(zone.Slug, zone.Name ?? zone.Slug, zone.Count))
            .ToList();

        var topSavedZones = (await dbContext.SavedZones.AsNoTracking()
                .GroupBy(zone => zone.ZoneSlug)
                .Select(group => new { Slug = group.Key, Name = group.Max(zone => zone.Name), Count = group.Count() })
                .OrderByDescending(zone => zone.Count)
                .ThenBy(zone => zone.Slug)
                .Take(period.TopZoneCount)
                .ToListAsync(cancellationToken))
            .Select(zone => new AdminZoneCount(zone.Slug, zone.Name ?? zone.Slug, zone.Count))
            .ToList();

        return new AdminStatsSnapshot(
            users,
            activity,
            community,
            recentRegistrationDates,
            topAnalyzedZones,
            topSavedZones,
            await GetCriteriaUsageAsync(cancellationToken),
            await GetOperationSplitAsync(cancellationToken));
    }

    private async Task<AdminUserStats> GetUserStatsAsync(AdminStatsPeriod period, CancellationToken cancellationToken)
    {
        var users = dbContext.Users.AsNoTracking();

        return new AdminUserStats(
            Total: await users.CountAsync(cancellationToken),
            NewLast7Days: await users.CountAsync(user => user.CreatedAt >= period.Since7Days, cancellationToken),
            NewLast30Days: await users.CountAsync(user => user.CreatedAt >= period.Since30Days, cancellationToken),
            Administrators: await users.CountAsync(user => user.IsAdministrator, cancellationToken),
            WithProfile: await dbContext.Contexts.AsNoTracking()
                .Where(context => context.Type == ContextType.Profile)
                .Select(context => context.UserId)
                .Distinct()
                .CountAsync(cancellationToken),
            WithAnalysis: await dbContext.Analyses.AsNoTracking()
                .Select(analysis => analysis.UserId)
                .Distinct()
                .CountAsync(cancellationToken));
    }

    private async Task<AdminActivityStats> GetActivityStatsAsync(AdminStatsPeriod period, CancellationToken cancellationToken)
    {
        var analyses = dbContext.Analyses.AsNoTracking();
        var groups = dbContext.Groups.AsNoTracking();
        var invitations = dbContext.GroupInvitations.AsNoTracking();
        var guestReports = dbContext.GuestReportSends.AsNoTracking();
        // Groups store their creation date as a UTC DateTime.
        var groupsSince = period.Since30Days.UtcDateTime;

        return new AdminActivityStats(
            Analyses: await analyses.CountAsync(cancellationToken),
            AnalysesLast30Days: await analyses.CountAsync(analysis => analysis.CreatedAt >= period.Since30Days, cancellationToken),
            FavoriteAnalyses: await analyses.CountAsync(analysis => analysis.IsFavorite, cancellationToken),
            Groups: await groups.CountAsync(cancellationToken),
            GroupsLast30Days: await groups.CountAsync(group => group.CreatedAt >= groupsSince, cancellationToken),
            GroupInvitations: await invitations.CountAsync(cancellationToken),
            AcceptedGroupInvitations: await invitations.CountAsync(invitation => invitation.Status == InvitationStatus.Accepted, cancellationToken),
            SavedZones: await dbContext.SavedZones.AsNoTracking().CountAsync(cancellationToken),
            GuestReports: await guestReports.CountAsync(cancellationToken),
            GuestReportsLast30Days: await guestReports.CountAsync(report => report.SentAt >= period.Since30Days, cancellationToken));
    }

    private async Task<AdminCommunityStats> GetCommunityStatsAsync(AdminStatsPeriod period, CancellationToken cancellationToken)
    {
        var reviews = dbContext.Reviews.AsNoTracking();
        var questions = dbContext.Questions.AsNoTracking();
        var contributions = dbContext.Contributions.AsNoTracking();

        return new AdminCommunityStats(
            Reviews: await reviews.CountAsync(cancellationToken),
            ReviewsLast30Days: await reviews.CountAsync(review => review.CreatedAt >= period.Since30Days, cancellationToken),
            // OverallScore is not stored, so the average is computed from the aspect scores. Null when there are no reviews.
            AverageReviewScore: await reviews
                .Select(review => (double?)(review.SafetyScore + review.TransitScore + review.ConnectivityScore
                    + review.CommerceScore + review.GreenSpaceScore) / 5.0)
                .AverageAsync(cancellationToken),
            Questions: await questions.CountAsync(cancellationToken),
            UnansweredQuestions: await questions.CountAsync(question => !question.Answers.Any(), cancellationToken),
            Answers: await dbContext.Answers.AsNoTracking().CountAsync(cancellationToken),
            PendingContributions: await contributions.CountAsync(contribution => contribution.Status == ContributionStatus.Pending, cancellationToken),
            ApprovedContributions: await contributions.CountAsync(contribution => contribution.Status == ContributionStatus.Approved, cancellationToken),
            RejectedContributions: await contributions.CountAsync(contribution => contribution.Status == ContributionStatus.Rejected, cancellationToken),
            CommunityPois: await dbContext.CommunityPois.AsNoTracking().CountAsync(poi => !poi.Removed, cancellationToken));
    }

    // Criteria of saved analyses (their contexts are copies of the profile at the time of the analysis).
    private async Task<List<AdminCriterionCount>> GetCriteriaUsageAsync(CancellationToken cancellationToken) =>
        (await dbContext.ContextCriteria.AsNoTracking()
            .Where(criterion => criterion.Context!.Type == ContextType.Analysis)
            .GroupBy(criterion => new { criterion.CriterionId, criterion.Criterion!.Name })
            .Select(group => new
            {
                group.Key.CriterionId,
                group.Key.Name,
                Count = group.Count(),
                HighPriorityCount = group.Count(criterion => criterion.Priority == HighPriority),
            })
            .OrderByDescending(criterion => criterion.Count)
            .ThenBy(criterion => criterion.CriterionId)
            .ToListAsync(cancellationToken))
        .Select(criterion => new AdminCriterionCount(criterion.CriterionId, criterion.Name, criterion.Count, criterion.HighPriorityCount))
        .ToList();

    private async Task<AdminOperationSplit> GetOperationSplitAsync(CancellationToken cancellationToken)
    {
        var analysisContexts = dbContext.Contexts.AsNoTracking().Where(context => context.Type == ContextType.Analysis);
        var purchase = await analysisContexts.CountAsync(context => context.Operation == PurchaseOperation, cancellationToken);
        var total = await analysisContexts.CountAsync(cancellationToken);
        return new AdminOperationSplit(Rent: total - purchase, Purchase: purchase);
    }
}

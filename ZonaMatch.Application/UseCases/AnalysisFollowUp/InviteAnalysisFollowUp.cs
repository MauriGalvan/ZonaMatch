using ZonaMatch.Application.DTOs.AnalysisFollowUp.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Application.UseCases.AnalysisFollowUp;

public sealed class InviteAnalysisFollowUp(IAnalysisFollowUpRepository repository, TimeProvider clock)
{
    // POST reserves a single invitation. Reopening the page returns the same unanswered card.
    public async Task<AnalysisFollowUpResponse?> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preferences = await repository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is { SurveysEnabled: false })
            return null;

        var active = await repository.GetActiveAsync(userId, cancellationToken);
        if (active is not null)
            return ToResponse(active);

        var now = clock.GetUtcNow();
        if (preferences?.NextInvitationAt > now)
            return null;

        var analysis = await repository.GetEligibleAnalysisAsync(
            userId, now.AddDays(-AnalysisFollowUpRules.MinimumAnalysisAgeDays), now, cancellationToken);
        if (analysis is null)
            return null;

        if (preferences is null)
        {
            preferences = new UserFeedbackPreferences { UserId = userId };
            repository.Add(preferences);
        }

        var followUp = await repository.GetForAnalysisAsync(analysis.Id, userId, cancellationToken);
        if (followUp is null)
        {
            followUp = new FollowUp { UserId = userId, AnalysisId = analysis.Id, Analysis = analysis };
            repository.Add(followUp);
        }

        followUp.InvitedAt = now;
        followUp.PostponedUntil = null;
        preferences.NextInvitationAt = now.AddDays(AnalysisFollowUpRules.InvitationIntervalDays);
        await repository.SaveChangesAsync(cancellationToken);
        return ToResponse(followUp);
    }

    private static AnalysisFollowUpResponse ToResponse(FollowUp followUp) =>
        new(followUp.Id, followUp.AnalysisId, followUp.Analysis.Name, followUp.Analysis.CreatedAt);
}

using ZonaMatch.Application.DTOs.Nps;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Nps;

public sealed class NpsSurveys(INpsRepository nps, IAnalysisFollowUpRepository feedback, TimeProvider clock)
{
    public async Task<NpsInvitationResponse?> InviteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preferences = await feedback.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is { SurveysEnabled: false } || preferences?.ActiveSurvey == "follow-up") return null;
        // Also recognizes follow-ups invited before the shared reservation was introduced.
        if (await feedback.GetActiveAsync(userId, cancellationToken) is not null) return null;
        var active = await nps.GetActiveAsync(userId, cancellationToken);
        if (active is not null) return new(active.Id);
        var now = clock.GetUtcNow();
        if (preferences?.NextInvitationAt > now) return null;
        if (!await nps.IsExperiencedAsync(userId, now.AddDays(-7), cancellationToken)) return null;
        if (await nps.IsInCooldownAsync(userId, now.AddDays(-90), now.AddDays(-30), cancellationToken)) return null;
        // A due follow-up takes priority; its card remains in Mis análisis.
        if (await feedback.GetEligibleAnalysisAsync(userId, now.AddDays(-AnalysisFollowUpRules.MinimumAnalysisAgeDays), now, cancellationToken) is not null) return null;
        if (preferences is null)
        {
            preferences = new UserFeedbackPreferences { UserId = userId };
            feedback.Add(preferences);
        }
        var survey = new NpsSurvey { UserId = userId, InvitedAt = now };
        nps.Add(survey);
        preferences.ActiveSurvey = "nps";
        preferences.NextInvitationAt = now.AddDays(AnalysisFollowUpRules.InvitationIntervalDays);
        await feedback.SaveChangesAsync(cancellationToken);
        return new(survey.Id);
    }

    public async Task SubmitAsync(Guid id, Guid userId, SubmitNpsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Score is not (>= 0 and <= 10) || request.Comment?.Length > 2000)
            throw new BadRequestException("Elegí una puntuación de 0 a 10 y un comentario de hasta 2.000 caracteres.");
        var (survey, preferences) = await GetActiveAsync(id, userId, cancellationToken);
        var now = clock.GetUtcNow();
        survey.Score = request.Score;
        survey.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        survey.CompletedAt = now;
        preferences.ActiveSurvey = null;
        preferences.NextInvitationAt = now.AddDays(AnalysisFollowUpRules.InvitationIntervalDays);
        await feedback.SaveChangesAsync(cancellationToken);
    }

    public async Task PostponeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var (survey, preferences) = await GetActiveAsync(id, userId, cancellationToken);
        var now = clock.GetUtcNow();
        survey.PostponedAt = now;
        preferences.ActiveSurvey = null;
        preferences.NextInvitationAt = now.AddDays(AnalysisFollowUpRules.InvitationIntervalDays);
        await feedback.SaveChangesAsync(cancellationToken);
    }

    private async Task<(NpsSurvey, UserFeedbackPreferences)> GetActiveAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var survey = await nps.GetAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("La encuesta no existe.");
        var preferences = await feedback.GetPreferencesAsync(userId, cancellationToken);
        if (survey.CompletedAt is not null || survey.PostponedAt is not null || preferences is not { SurveysEnabled: true, ActiveSurvey: "nps" })
            throw new ConflictException("La invitación ya no está disponible. Actualizá la página.");
        return (survey, preferences);
    }
}

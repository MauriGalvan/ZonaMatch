using ZonaMatch.Application.DTOs.AnalysisFollowUp.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.AnalysisFollowUp;

public sealed class UpdateFeedbackPreferences(IAnalysisFollowUpRepository repository)
{
    public async Task<FeedbackPreferencesResponse> ExecuteAsync(Guid userId, bool surveysEnabled, CancellationToken cancellationToken = default)
    {
        var preferences = await repository.GetPreferencesAsync(userId, cancellationToken);
        if (preferences is null)
        {
            preferences = new UserFeedbackPreferences { UserId = userId };
            repository.Add(preferences);
        }
        preferences.SurveysEnabled = surveysEnabled;
        await repository.SaveChangesAsync(cancellationToken);
        return new FeedbackPreferencesResponse(preferences.SurveysEnabled);
    }
}

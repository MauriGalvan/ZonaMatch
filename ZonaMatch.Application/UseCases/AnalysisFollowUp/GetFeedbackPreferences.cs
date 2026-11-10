using ZonaMatch.Application.DTOs.AnalysisFollowUp.Responses;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.AnalysisFollowUp;

public sealed class GetFeedbackPreferences(IAnalysisFollowUpRepository repository)
{
    public async Task<FeedbackPreferencesResponse> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default) =>
        new((await repository.GetPreferencesAsync(userId, cancellationToken))?.SurveysEnabled ?? true);
}

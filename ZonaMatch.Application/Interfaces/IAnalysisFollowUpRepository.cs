using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces;

public interface IAnalysisFollowUpRepository
{
    Task<UserFeedbackPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AnalysisFollowUp?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AnalysisFollowUp?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<AnalysisFollowUp?> GetForAnalysisAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default);
    Task<Analysis?> GetEligibleAnalysisAsync(Guid userId, DateTimeOffset createdBefore, DateTimeOffset now, CancellationToken cancellationToken = default);
    void Add(AnalysisFollowUp followUp);
    void Add(UserFeedbackPreferences preferences);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

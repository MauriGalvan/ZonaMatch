using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using FollowUp = ZonaMatch.Domain.Entities.AnalysisFollowUp;

namespace ZonaMatch.Tests.UseCases.AnalysisFollowUp;

// In-memory repository and deterministic clock shared by the follow-up use-case tests.
internal sealed class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeAnalysisFollowUpRepository : IAnalysisFollowUpRepository
{
    public List<Analysis> Analyses { get; } = [];
    public List<FollowUp> FollowUps { get; } = [];
    private readonly List<UserFeedbackPreferences> _preferences = [];
    public int SaveCount { get; private set; }
    public Task<UserFeedbackPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_preferences.SingleOrDefault(p => p.UserId == userId));
    public Task<FollowUp?> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FollowUps.FirstOrDefault(f => f.UserId == userId && f.CompletedAt is null && f.PostponedUntil is null));
    public Task<FollowUp?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FollowUps.SingleOrDefault(f => f.Id == id && f.UserId == userId));
    public Task<FollowUp?> GetForAnalysisAsync(Guid analysisId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(FollowUps.SingleOrDefault(f => f.AnalysisId == analysisId && f.UserId == userId));
    public Task<Analysis?> GetEligibleAnalysisAsync(Guid userId, DateTimeOffset createdBefore, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult(Analyses.Where(a => a.UserId == userId && a.OriginalAnalysisId is null && a.CreatedAt <= createdBefore)
            .Where(a => !FollowUps.Any(f => f.AnalysisId == a.Id && (f.CompletedAt is not null || f.PostponedUntil > now)))
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).FirstOrDefault());
    public void Add(FollowUp followUp) => FollowUps.Add(followUp);
    public void Add(UserFeedbackPreferences preferences) => _preferences.Add(preferences);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
}

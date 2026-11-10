using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.UseCases.SavedAnalysis;

internal static class AnalysisTrashTestSupport
{
    public static readonly DateTimeOffset CurrentTime = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    public static Analysis CreateAnalysis(
        Guid userId,
        string name,
        DateTimeOffset? deletedAt = null,
        Guid? originalAnalysisId = null,
        string? groupScoresJson = null) => new()
    {
        UserId = userId,
        Name = name,
        DeletedAt = deletedAt,
        OriginalAnalysisId = originalAnalysisId,
        GroupScoresJson = groupScoresJson,
        Context = new Context { UserId = userId, Type = ContextType.Analysis },
    };

    public sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}

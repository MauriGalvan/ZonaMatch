using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalysis;

public sealed class PurgeExpiredAnalyses(IAnalysisRepository analyses, TimeProvider clock)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var deletedBefore = clock.GetUtcNow() - Analysis.TrashRetentionPeriod;
        var expiredAnalyses = await analyses.GetExpiredTrashedAsync(deletedBefore, cancellationToken);
        if (expiredAnalyses.Count == 0)
            return 0;

        foreach (var analysis in expiredAnalyses.OrderBy(item => item.OriginalAnalysisId is null))
            analyses.PermanentlyDelete(analysis);

        await analyses.SaveChangesAsync(cancellationToken);
        return expiredAnalyses.Count;
    }
}

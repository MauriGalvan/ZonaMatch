using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis;

public sealed class EmptyAnalysisTrash(IAnalysisRepository analyses)
{
    public async Task ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var trashedAnalyses = await analyses.GetDeletedForUserAsync(userId, cancellationToken);
        if (trashedAnalyses.Count == 0)
            return;

        foreach (var analysis in trashedAnalyses.OrderBy(item => item.OriginalAnalysisId is null))
            analyses.PermanentlyDelete(analysis);

        await analyses.SaveChangesAsync(cancellationToken);
    }
}

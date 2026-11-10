using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalysis;

public sealed class ListAnalysisTrash(IAnalysisRepository analyses, TimeProvider clock)
{
    public async Task<IReadOnlyList<TrashedAnalysisResponse>> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var deletedAfter = now - Analysis.TrashRetentionPeriod;
        var trashedAnalyses = await analyses.GetTrashedForUserAsync(userId, deletedAfter, cancellationToken);

        return trashedAnalyses
            .Select(analysis =>
            {
                var deletedAt = analysis.DeletedAt!.Value;
                return new TrashedAnalysisResponse(
                    analysis.Id,
                    analysis.Name,
                    analysis.OriginalAnalysisId,
                    analysis.GroupScoresJson is not null,
                    deletedAt,
                    deletedAt.Add(Analysis.TrashRetentionPeriod));
            })
            .ToList();
    }
}

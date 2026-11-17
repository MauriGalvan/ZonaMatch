using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalysis;

public sealed class RestoreAnalysis(IAnalysisRepository analyses, TimeProvider clock)
{
    public async Task ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var analysis = await analyses.GetTrashedAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("El análisis no existe en la papelera.");

        var now = clock.GetUtcNow();
        if (analysis.DeletedAt!.Value <= now - Analysis.TrashRetentionPeriod)
            throw new NotFoundException("El plazo para recuperar este análisis venció.");

        if (analysis.OriginalAnalysisId is null)
        {
            await RestoreOriginalAndVersionsAsync(analysis, cancellationToken);
        }
        else
        {
            var original = await analyses.GetIncludingDeletedAsync(
                analysis.OriginalAnalysisId.Value,
                userId,
                cancellationToken);

            if (original?.DeletedAt is not null)
                await RestoreOriginalAndVersionsAsync(original, cancellationToken);
            else
                analysis.DeletedAt = null;
        }

        await analyses.SaveChangesAsync(cancellationToken);
    }

    private async Task RestoreOriginalAndVersionsAsync(Analysis original, CancellationToken cancellationToken)
    {
        original.DeletedAt = null;

        var versions = await analyses.ListVersionsIncludingDeletedAsync(original.Id, cancellationToken);
        foreach (var version in versions)
            version.DeletedAt = null;
    }
}

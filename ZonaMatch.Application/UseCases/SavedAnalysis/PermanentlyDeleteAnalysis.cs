using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis;

public sealed class PermanentlyDeleteAnalysis(IAnalysisRepository analyses)
{
    public async Task ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var analysis = await analyses.GetTrashedAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("El análisis no existe en la papelera.");

        if (analysis.OriginalAnalysisId is null)
        {
            var versions = await analyses.ListVersionsIncludingDeletedAsync(analysis.Id, cancellationToken);
            foreach (var version in versions)
                analyses.PermanentlyDelete(version);
        }

        analyses.PermanentlyDelete(analysis);
        await analyses.SaveChangesAsync(cancellationToken);
    }
}

using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    // Moves an analysis to the user's trash. Deleting an original also moves all its versions.
    public class DeleteAnalysis
    {
        private readonly IAnalysisRepository _analysisRepository;
        private readonly TimeProvider _clock;

        public DeleteAnalysis(IAnalysisRepository analysisRepository, TimeProvider clock)
        {
            _analysisRepository = analysisRepository;
            _clock = clock;
        }

        public async Task ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            var analysis = await _analysisRepository.GetAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var deletedAt = _clock.GetUtcNow();
            analysis.DeletedAt = deletedAt;

            if (analysis.OriginalAnalysisId is null)
            {
                var versions = await _analysisRepository.ListVersionsIncludingDeletedAsync(analysis.Id, cancellationToken);
                foreach (var version in versions)
                    version.DeletedAt = deletedAt;
            }

            await _analysisRepository.SaveChangesAsync(cancellationToken);
        }
    }
}

using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    // Deletes an analysis and its criteria snapshot. Deleting an original also deletes its versions.
    public class DeleteAnalysis
    {
        private readonly IAnalysisRepository _analysisRepository;

        public DeleteAnalysis(IAnalysisRepository analysisRepository)
        {
            _analysisRepository = analysisRepository;
        }

        public async Task ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            var analysis = await _analysisRepository.GetAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            if (analysis.OriginalAnalysisId is null)
            {
                foreach (var version in await _analysisRepository.ListVersionsAsync(analysis.Id, cancellationToken))
                    _analysisRepository.Delete(version);
            }

            _analysisRepository.Delete(analysis);
            await _analysisRepository.SaveChangesAsync(cancellationToken);
        }
    }
}

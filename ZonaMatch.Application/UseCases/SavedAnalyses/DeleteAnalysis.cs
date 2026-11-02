using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
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
            var analysis = await _analysisRepository.ObtenerAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            if (analysis.AnalisisOrigenId is null)
            {
                foreach (var version in await _analysisRepository.ListarVersionesAsync(analysis.Id, cancellationToken))
                    _analysisRepository.Eliminar(version);
            }

            _analysisRepository.Eliminar(analysis);
            await _analysisRepository.GuardarCambiosAsync(cancellationToken);
        }
    }
}

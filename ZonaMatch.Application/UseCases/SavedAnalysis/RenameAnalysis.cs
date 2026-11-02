using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    // Renames an analysis or a version. Criteria are adjusted by creating a new version.
    public class RenameAnalysis
    {
        private readonly IAnalysisRepository _analysisRepository;
        private readonly TimeProvider _clock;

        public RenameAnalysis(IAnalysisRepository analysisRepository, TimeProvider clock)
        {
            _analysisRepository = analysisRepository;
            _clock = clock;
        }

        public async Task<AnalysisResponse> ExecuteAsync(
            Guid id, Guid userId, RenameAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            var analysis = await _analysisRepository.GetAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            analysis.Name = request.Name.Trim();
            analysis.UpdatedAt = _clock.GetUtcNow();
            await _analysisRepository.SaveChangesAsync(cancellationToken);

            var versions = analysis.OriginalAnalysisId is null
                ? await _analysisRepository.ListVersionsAsync(analysis.Id, cancellationToken)
                : [];

            return AnalysisMapping.ToResponse(analysis, versions);
        }
    }
}

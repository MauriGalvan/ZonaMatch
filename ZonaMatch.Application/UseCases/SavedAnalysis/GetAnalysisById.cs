using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    // Returns one of the user's analyses and, for originals, its versions.
    public class GetAnalysisById
    {
        private readonly IAnalysisRepository _analysisRepository;

        public GetAnalysisById(IAnalysisRepository analysisRepository)
        {
            _analysisRepository = analysisRepository;
        }

        public async Task<AnalysisResponse> ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            var analysis = await _analysisRepository.GetAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var versions = analysis.OriginalAnalysisId is null
                ? await _analysisRepository.ListVersionsAsync(analysis.Id, cancellationToken)
                : [];

            return AnalysisMapping.ToResponse(analysis, versions);
        }
    }
}

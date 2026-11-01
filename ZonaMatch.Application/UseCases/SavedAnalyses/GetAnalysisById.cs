using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Returns one of the user's analyses and, for originals, its versions.
    public class GetAnalysisById
    {
        private readonly IAnalisisRepository _analysisRepository;

        public GetAnalysisById(IAnalisisRepository analysisRepository)
        {
            _analysisRepository = analysisRepository;
        }

        public async Task<AnalysisResponse> ExecuteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            var analysis = await _analysisRepository.ObtenerAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var versions = analysis.AnalisisOrigenId is null
                ? await _analysisRepository.ListarVersionesAsync(analysis.Id, cancellationToken)
                : [];

            return AnalysisMapping.ToResponse(analysis, versions);
        }
    }
}

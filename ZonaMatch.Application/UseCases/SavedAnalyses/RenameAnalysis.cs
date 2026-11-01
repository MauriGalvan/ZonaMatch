using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Renames an analysis or a version. Criteria are adjusted by creating a new version.
    public class RenameAnalysis
    {
        private readonly IAnalisisRepository _analysisRepository;
        private readonly TimeProvider _clock;

        public RenameAnalysis(IAnalisisRepository analysisRepository, TimeProvider clock)
        {
            _analysisRepository = analysisRepository;
            _clock = clock;
        }

        public async Task<AnalysisResponse> ExecuteAsync(
            Guid id, Guid userId, RenameAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            var analysis = await _analysisRepository.ObtenerAsync(id, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            analysis.Nombre = request.Name.Trim();
            analysis.FechaActualizacion = _clock.GetUtcNow();
            await _analysisRepository.GuardarCambiosAsync(cancellationToken);

            var versions = analysis.AnalisisOrigenId is null
                ? await _analysisRepository.ListarVersionesAsync(analysis.Id, cancellationToken)
                : [];

            return AnalysisMapping.ToResponse(analysis, versions);
        }
    }
}

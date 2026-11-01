using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Saves an original analysis with the user's selected zones and a snapshot of their profile.
    // The user's saved profile is required before creating an analysis.
    public class CreateAnalysis
    {
        private readonly IAnalisisRepository _analysisRepository;
        private readonly IPerfilRepository _profileRepository;
        private readonly IZonaRepository _zoneRepository;
        private readonly TimeProvider _clock;
        private readonly IAnalysisResultCalculator _resultCalculator;

        public CreateAnalysis(
            IAnalisisRepository analysisRepository, IPerfilRepository profileRepository, IZonaRepository zoneRepository,
            TimeProvider clock, IAnalysisResultCalculator resultCalculator)
        {
            _analysisRepository = analysisRepository;
            _profileRepository = profileRepository;
            _zoneRepository = zoneRepository;
            _clock = clock;
            _resultCalculator = resultCalculator;
        }

        public async Task<AnalysisResponse> ExecuteAsync(
            Guid userId, CreateAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            var profile = await _profileRepository.ObtenerDeUsuarioAsync(userId, cancellationToken)
                ?? throw new NotFoundException("Todavía no armaste tu perfil.");

            var zones = await AnalysisMapping.BuildZonesAsync(request.Zones, _zoneRepository, cancellationToken);
            var now = _clock.GetUtcNow();

            var analysis = new Analisis
            {
                UsuarioId = userId,
                Nombre = request.Name.Trim(),
                Contexto = profile.CopiarParaAnalisis(now),
                FechaCreacion = now,
                FechaActualizacion = now,
                Zonas = zones,
            };

            // The result shown when it is opened again, until the user recalculates it
            if (await _resultCalculator.CalculateAsync(analysis, cancellationToken) is { } result)
            {
                analysis.ResultadoJson = AnalysisResultCalculator.Serialize(result);
                analysis.FechaResultado = now;
            }

            _analysisRepository.Agregar(analysis);
            await _analysisRepository.GuardarCambiosAsync(cancellationToken);

            return AnalysisMapping.ToResponse(analysis);
        }
    }
}

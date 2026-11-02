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
        private readonly IAnalysisRepository _analysisRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly IZoneRepository _zoneRepository;
        private readonly TimeProvider _clock;
        private readonly IAnalysisResultCalculator _resultCalculator;

        public CreateAnalysis(
            IAnalysisRepository analysisRepository, IProfileRepository profileRepository, IZoneRepository zoneRepository,
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
            var profile = await _profileRepository.GetByUserAsync(userId, cancellationToken)
                ?? throw new NotFoundException("Todavía no armaste tu perfil.");

            var zones = await AnalysisMapping.BuildZonesAsync(request.Zones, _zoneRepository, cancellationToken);
            var now = _clock.GetUtcNow();

            var analysis = new Analysis
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Context = profile.CopyForAnalysis(now),
                CreatedAt = now,
                UpdatedAt = now,
                Zones = zones,
            };

            // The result shown when it is opened again, until the user recalculates it
            if (await _resultCalculator.CalculateAsync(analysis, cancellationToken) is { } result)
            {
                analysis.ResultJson = AnalysisResultCalculator.Serialize(result);
                analysis.ResultGeneratedAt = now;
            }

            _analysisRepository.Add(analysis);
            await _analysisRepository.SaveChangesAsync(cancellationToken);

            return AnalysisMapping.ToResponse(analysis);
        }
    }
}

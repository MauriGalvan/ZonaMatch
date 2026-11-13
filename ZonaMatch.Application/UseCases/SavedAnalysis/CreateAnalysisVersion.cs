using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalysis
{
    // Adjusts criteria for one analysis and saves a derived version; the original and the profile stay unchanged.
    // Versions always belong to the original, even when another version was adjusted.
    public class CreateAnalysisVersion
    {
        private const int MaxNameLength = 50;

        private readonly IAnalysisRepository _analysisRepository;
        private readonly ICriterionRepository _criteriaRepository;
        private readonly IZoneRepository _zoneRepository;
        private readonly TimeProvider _clock;
        private readonly IAnalysisResultCalculator _resultCalculator;

        public CreateAnalysisVersion(
            IAnalysisRepository analysisRepository, ICriterionRepository criteriaRepository, IZoneRepository zoneRepository,
            TimeProvider clock, IAnalysisResultCalculator resultCalculator)
        {
            _analysisRepository = analysisRepository;
            _criteriaRepository = criteriaRepository;
            _zoneRepository = zoneRepository;
            _clock = clock;
            _resultCalculator = resultCalculator;
        }

        public async Task<AnalysisResponse> ExecuteAsync(
            Guid analysisId, Guid userId, CreateAnalysisVersionRequest request, CancellationToken cancellationToken = default)
        {
            var sourceAnalysis = await _analysisRepository.GetAsync(analysisId, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var original = sourceAnalysis.OriginalAnalysisId is { } originalId
                ? await _analysisRepository.GetAsync(originalId, userId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.")
                : sourceAnalysis;

            var criteriaCatalog = (await _criteriaRepository.ListAsync(cancellationToken))
                .Where(criterion => criterion.ForHousehold)
                .ToList();
            var now = _clock.GetUtcNow();

            var context = new Context
            {
                UserId = userId,
                Type = ContextType.Analysis,
                CreatedAt = now,
                UpdatedAt = now,
            };
            AnalysisCriteriaBuilder.Apply(context, request.Criterion, criteriaCatalog);

            // A version adjusts the analysis it comes from; switching between renting and buying needs a new analysis.
            if (context.Operation != sourceAnalysis.Context.Operation)
                throw new BadRequestException("No se puede cambiar entre alquilar y comprar al ajustar un análisis. Creá un análisis nuevo.");

            var zones = request.Zones is { } requestedZones
                ? await AnalysisMapping.BuildZonesAsync(requestedZones, _zoneRepository, cancellationToken)
                : sourceAnalysis.Zones
                    .OrderBy(z => z.Order)
                    .Select(z => z.Copy())
                    .ToList();

            var versions = await _analysisRepository.ListVersionsAsync(original.Id, cancellationToken);

            var version = new Analysis
            {
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(request.Name)
                    ? BuildVersionName(original.Name, versions.Count + 2)
                    : request.Name.Trim(),
                Context = context,
                OriginalAnalysisId = original.Id,
                CreatedAt = now,
                UpdatedAt = now,
                Zones = zones,
            };

            if (await _resultCalculator.CalculateAsync(version, cancellationToken) is { } result)
            {
                version.ResultJson = AnalysisResultCalculator.Serialize(result);
                version.ResultGeneratedAt = now;
            }

            _analysisRepository.Add(version);
            await _analysisRepository.SaveChangesAsync(cancellationToken);

            return AnalysisMapping.ToResponse(version);
        }

        // "Near work (2)": the original counts as version 1; trim the name to stay within the maximum length.
        public static string BuildVersionName(string originalName, int versionNumber)
        {
            var suffix = $" ({versionNumber})";
            var baseName = originalName.Length + suffix.Length > MaxNameLength
                ? originalName[..(MaxNameLength - suffix.Length)].TrimEnd()
                : originalName;

            return baseName + suffix;
        }
    }
}

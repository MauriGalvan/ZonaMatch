using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.DTOs.Analysis.Requests;
using ZonaMatch.Application.DTOs.Analysis.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.SavedAnalyses
{
    // Adjusts criteria for one analysis and saves a derived version; the original and the profile stay unchanged.
    // Versions always belong to the original, even when another version was adjusted.
    public class CreateAnalysisVersion
    {
        private const int MaxNameLength = 50;

        private readonly IAnalisisRepository _analysisRepository;
        private readonly ICriterioRepository _criteriaRepository;
        private readonly IZonaRepository _zoneRepository;
        private readonly TimeProvider _clock;
        private readonly IAnalysisResultCalculator _resultCalculator;

        public CreateAnalysisVersion(
            IAnalisisRepository analysisRepository, ICriterioRepository criteriaRepository, IZonaRepository zoneRepository,
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
            var sourceAnalysis = await _analysisRepository.ObtenerAsync(analysisId, userId, cancellationToken)
                ?? throw new NotFoundException("El análisis no existe.");

            var original = sourceAnalysis.AnalisisOrigenId is { } originalId
                ? await _analysisRepository.ObtenerAsync(originalId, userId, cancellationToken) ?? throw new NotFoundException("El análisis no existe.")
                : sourceAnalysis;

            var criteriaCatalog = await _criteriaRepository.ListarAsync(cancellationToken);
            var now = _clock.GetUtcNow();

            var context = new Contexto
            {
                UsuarioId = userId,
                Tipo = TipoContexto.Analisis,
                FechaCreacion = now,
                FechaActualizacion = now,
            };
            AnalysisCriteriaBuilder.Apply(context, request.Criterion, criteriaCatalog);

            var zones = request.Zones is { } requestedZones
                ? await AnalysisMapping.BuildZonesAsync(requestedZones, _zoneRepository, cancellationToken)
                : sourceAnalysis.Zonas
                    .OrderBy(z => z.Orden)
                    .Select(z => z.Copiar())
                    .ToList();

            var versions = await _analysisRepository.ListarVersionesAsync(original.Id, cancellationToken);

            var version = new Analisis
            {
                UsuarioId = userId,
                Nombre = string.IsNullOrWhiteSpace(request.Name)
                    ? BuildVersionName(original.Nombre, versions.Count + 2)
                    : request.Name.Trim(),
                Contexto = context,
                AnalisisOrigenId = original.Id,
                FechaCreacion = now,
                FechaActualizacion = now,
                Zonas = zones,
            };

            if (await _resultCalculator.CalculateAsync(version, cancellationToken) is { } result)
            {
                version.ResultadoJson = AnalysisResultCalculator.Serialize(result);
                version.FechaResultado = now;
            }

            _analysisRepository.Agregar(version);
            await _analysisRepository.GuardarCambiosAsync(cancellationToken);

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
